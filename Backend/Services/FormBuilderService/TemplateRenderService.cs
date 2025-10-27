using ActoEngine.WebApi.Repositories;
using Scriban;

namespace ActoEngine.WebApi.Services.FormBuilderService
{
    public class TemplateRenderService
    {
        private readonly CodeTemplateRepository _templateRepository;
        private readonly ILogger<TemplateRenderService> _logger;

        // Cache compiled templates for performance
        private static readonly Dictionary<string, Template> _compiledTemplates = [];
        private static readonly object _cacheLock = new();

        public TemplateRenderService(
            CodeTemplateRepository templateRepository,
            ILogger<TemplateRenderService> logger)
        {
            _templateRepository = templateRepository;
            _logger = logger;
        }

        public async Task<string> RenderTemplateAsync(string templateType, string framework, object context, string? version = null)
        {
            try
            {
                // Get template content
                var template = await _templateRepository.GetTemplateAsync(templateType, framework, version);
                if (template == null)
                {
                    throw new InvalidOperationException($"Template not found: {templateType}/{framework}");
                }

                // Get or compile template
                var cacheKey = $"{templateType}_{framework}_{template.Version}";
                Template? compiledTemplate = null;

                lock (_cacheLock)
                {
                    if (!_compiledTemplates.TryGetValue(cacheKey, out compiledTemplate))
                    {
                        compiledTemplate = Template.Parse(template.TemplateContent);
                        _compiledTemplates[cacheKey] = compiledTemplate;

                        _logger.LogInformation("Compiled and cached template: {CacheKey}", cacheKey);
                    }
                }

                // Render template
                var result = await compiledTemplate!.RenderAsync(context);

                _logger.LogInformation("Rendered template: {TemplateType}/{Framework} v{TemplateVersion}",
                    templateType, framework, template.Version);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering template: {TemplateType}/{Framework}", templateType, framework);
                throw new TemplateRenderException($"Failed to render template {templateType}/{framework}", ex.Message);
            }
        }

        public async Task<string> RenderTemplateByNameAsync(string templateName, object context)
        {
            try
            {
                // Get template by name
                var template = await _templateRepository.GetByNameAsync(templateName);
                if (template == null)
                {
                    throw new InvalidOperationException($"Template not found: {templateName}");
                }

                // Get or compile template
                var cacheKey = templateName;
                Template? compiledTemplate = null;

                lock (_cacheLock)
                {
                    if (!_compiledTemplates.TryGetValue(cacheKey, out compiledTemplate))
                    {
                        compiledTemplate = Template.Parse(template.TemplateContent);
                        _compiledTemplates[cacheKey] = compiledTemplate;

                        _logger.LogInformation("Compiled and cached template: {TemplateName}", templateName);
                    }
                }

                // Render template
                var result = await compiledTemplate.RenderAsync(context);

                _logger.LogInformation("Rendered template: {TemplateName}", templateName);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering template: {TemplateName}", templateName);
                throw new TemplateRenderException($"Failed to render template {templateName}", ex.Message);
            }
        }

        public async Task<List<CodeTemplate>> GetAvailableTemplatesAsync(string? type = null, string? framework = null)
        {
            return await _templateRepository.GetTemplatesAsync(type, framework);
        }

        public async Task<CodeTemplate> SaveTemplateAsync(CodeTemplate template)
        {
            // Clear cache when template is updated
            lock (_cacheLock)
            {
                var keysToRemove = _compiledTemplates.Keys
                    .Where(k => k.Contains(template.TemplateName))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _compiledTemplates.Remove(key);
                    _logger.LogInformation("Cleared cached template: {CacheKey}", key);
                }
            }

            return await _templateRepository.SaveAsync(template);
        }

        // Clear all cached templates (useful for development/testing)
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _compiledTemplates.Clear();
                _logger.LogInformation("Cleared all template cache");
            }
        }
    }

    public class TemplateRenderException : Exception
    {
        public TemplateRenderException(string message) : base(message) { }

        public TemplateRenderException(string message, string innerExceptionMessage)
            : base(message, new Exception(innerExceptionMessage)) { }
    }
}