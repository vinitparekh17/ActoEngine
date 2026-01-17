using ActoEngine.WebApi.Features.SpBuilder;
using Scriban;
using SpCodeTemplate = ActoEngine.WebApi.Features.SpBuilder.CodeTemplate;

namespace ActoEngine.WebApi.Features.FormBuilder
{
    public interface ITemplateRenderService
    {
        /// <summary>
        /// Renders a template asynchronously using the specified template type, framework, and context data.
        /// </summary>
        /// <param name="templateType">The type of template to render</param>
        /// <param name="framework">The framework for which to render the template</param>
        /// <param name="context">The context data to be used in template rendering</param>
        /// <param name="version">Optional specific version of the template to use</param>
        /// <returns>The rendered template content as a string</returns>
        Task<string> RenderTemplateAsync(string templateType, string framework, object context, string? version = null);

        /// <summary>
        /// Renders a template by its unique name.
        /// </summary>
        /// <param name="templateName"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        Task<string> RenderTemplateByNameAsync(string templateName, object context);

        /// <summary>
        /// Gets a list of available templates, optionally filtered by type and framework.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="framework"></param>
        /// <returns></returns>
        Task<List<SpCodeTemplate>> GetAvailableTemplatesAsync(string? type = null, string? framework = null);

        /// <summary>
        /// Saves or updates a code template.
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        Task<SpCodeTemplate> SaveTemplateAsync(SpCodeTemplate template);
    }
    public class TemplateRenderService(
        ICodeTemplateRepository templateRepository,
        ILogger<TemplateRenderService> logger) : ITemplateRenderService
    {

        // Cache compiled templates for performance
        private static readonly Dictionary<string, Template> _compiledTemplates = [];
        private static readonly object _cacheLock = new();

        public async Task<string> RenderTemplateAsync(string templateType, string framework, object context, string? version = null)
        {
            try
            {
                // Get template content
                var template = await templateRepository.GetTemplateAsync(templateType, framework, version) ?? throw new InvalidOperationException($"Template not found: {templateType}/{framework}");

                // Get or compile template
                var cacheKey = $"{templateType}_{framework}_{template.Version}";
                Template? compiledTemplate = null;

                lock (_cacheLock)
                {
                    if (!_compiledTemplates.TryGetValue(cacheKey, out compiledTemplate))
                    {
                        compiledTemplate = Template.Parse(template.TemplateContent);
                        _compiledTemplates[cacheKey] = compiledTemplate;

                        logger.LogInformation("Compiled and cached template: {CacheKey}", cacheKey);
                    }
                }

                // Render template
                var result = await compiledTemplate!.RenderAsync(context);

                logger.LogInformation("Rendered template: {TemplateType}/{Framework} v{TemplateVersion}",
                    templateType, framework, template.Version);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error rendering template: {TemplateType}/{Framework}", templateType, framework);
                throw new TemplateRenderException($"Failed to render template {templateType}/{framework}", ex.Message);
            }
        }

        public async Task<string> RenderTemplateByNameAsync(string templateName, object context)
        {
            try
            {
                // Get template by name
                var template = await templateRepository.GetByNameAsync(templateName) ?? throw new InvalidOperationException($"Template not found: {templateName}");

                // Get or compile template
                var cacheKey = templateName;
                Template? compiledTemplate = null;

                lock (_cacheLock)
                {
                    if (!_compiledTemplates.TryGetValue(cacheKey, out compiledTemplate))
                    {
                        compiledTemplate = Template.Parse(template.TemplateContent);
                        _compiledTemplates[cacheKey] = compiledTemplate;

                        logger.LogInformation("Compiled and cached template: {TemplateName}", templateName);
                    }
                }

                // Render template
                var result = await compiledTemplate.RenderAsync(context);

                logger.LogInformation("Rendered template: {TemplateName}", templateName);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error rendering template: {TemplateName}", templateName);
                throw new TemplateRenderException($"Failed to render template {templateName}", ex.Message);
            }
        }

        public async Task<List<SpCodeTemplate>> GetAvailableTemplatesAsync(string? type = null, string? framework = null)
        {
            return await templateRepository.GetTemplatesAsync(type, framework);
        }

        public async Task<SpCodeTemplate> SaveTemplateAsync(SpCodeTemplate template)
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
                    logger.LogInformation("Cleared cached template: {CacheKey}", key);
                }
            }

            return await templateRepository.SaveAsync(template);
        }

        // Clear all cached templates (useful for development/testing)
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _compiledTemplates.Clear();
                logger.LogInformation("Cleared all template cache");
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