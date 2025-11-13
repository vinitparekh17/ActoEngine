using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using System.Text.Json;

namespace ActoEngine.WebApi.Services.FormBuilderService
{
    public interface IFormBuilderService
    {
        Task<SaveFormConfigResponse> SaveFormConfigAsync(SaveFormConfigRequest request);
        Task<FormConfig> LoadFormConfigAsync(LoadFormConfigRequest request);
        Task<List<FormConfigListItem>> GetFormConfigsAsync(int projectId);
        Task<GenerateFormResponse> GenerateFormAsync(GenerateFormRequest request);
        Task<bool> DeleteFormConfigAsync(string formId, int userId);
        Task<IEnumerable<object>> GetTemplatesAsync();
    }
    public class FormBuilderService(
        IFormConfigRepository formConfigRepo,
        ICodeTemplateRepository codeTemplateRepo,
        IGenerationHistoryRepository generationHistoryRepo,
        ITemplateRenderService templateService,
        ILogger<FormBuilderService> logger) : IFormBuilderService
    {
        private readonly IFormConfigRepository _formConfigRepo = formConfigRepo;
        private readonly ICodeTemplateRepository _codeTemplateRepo = codeTemplateRepo;
        private readonly IGenerationHistoryRepository _generationHistoryRepo = generationHistoryRepo;
        private readonly ITemplateRenderService _templateService = templateService;
        private readonly ILogger<FormBuilderService> _logger = logger;        // ============================================
        // Delete Form Configuration
        // ============================================
        public async Task<bool> DeleteFormConfigAsync(string formId, int userId)
        {
            return await _formConfigRepo.DeleteAsync(formId, userId);
        }

        // ============================================
        // Get Templates
        // ============================================
        public async Task<IEnumerable<object>> GetTemplatesAsync()
        {
            var templates = await _codeTemplateRepo.GetTemplatesAsync();
            // If you want to return as dynamic/object, you can map or cast as needed
            return templates.Select(t => new
            {
                t.Id,
                t.TemplateName,
                t.TemplateType,
                t.Framework,
                t.Version,
                t.Description,
                t.IsActive
            });
        }
        // ============================================
        // Save Form Configuration
        // ============================================
        public async Task<SaveFormConfigResponse> SaveFormConfigAsync(SaveFormConfigRequest request)
        {
            try
            {
                var configJson = JsonSerializer.Serialize(request.Config);
                var (config, formId) = await _formConfigRepo.SaveWithIdAsync(request.Config, configJson, request.ProjectId);

                return new SaveFormConfigResponse
                {
                    Success = true,
                    FormId = formId,
                    Config = config
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving form config");
                throw;
            }
        }

        // ============================================
        // Load Form Configuration
        // ============================================
        public async Task<FormConfig> LoadFormConfigAsync(LoadFormConfigRequest request)
        {
            try
            {
                var config = await _formConfigRepo.GetByIdOrNameAsync(request.FormId, request.UserId);

                if (config == null)
                {
                    throw new Exception($"Form configuration not found: {request.FormId}");
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading form config");
                throw;
            }
        }

        // ============================================
        // List Form Configurations
        // ============================================
        public async Task<List<FormConfigListItem>> GetFormConfigsAsync(int projectId)
        {
            try
            {
                return await _formConfigRepo.GetByProjectIdWithoutUserFilterAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting form configs");
                throw;
            }
        }

        // ============================================
        // Generate Form Code
        // ============================================
        public async Task<GenerateFormResponse> GenerateFormAsync(GenerateFormRequest request)
        {
            try
            {
                var config = request.Config ?? throw new Exception("Form configuration is required for generation");
                var tableName = config.TableName ?? throw new Exception("Config.TableName is required");
                var formName = config.FormName ?? throw new Exception("Config.FormName is required");

                // Prepare template context
                // Prepare template context
                var templateContext = new
                {
                    form = new  // <-- Wrap everything in a 'form' object
                    {
                        title = config.Title,
                        form_name = config.FormName,
                        description = config.Description,
                        options = config.Options != null ? new
                        {
                            generate_grid = config.Options.GenerateGrid

                        } : new
                        {
                            generate_grid = false
                        },
                        groups = config.Groups?.Select(g => new
                        {
                            id = g.Id,
                            title = g.Title,
                            description = g.Description,
                            collapsible = g.Collapsible,
                            fields = g.Fields?.Select(f => new
                            {
                                column_name = f.ColumnName,
                                label = f.Label,
                                input_type = (f.InputType ?? "text").ToLower(),
                                required = f.Required,
                                col_size = f.ColSize,
                                placeholder = f.Placeholder ?? "",
                                help_text = f.HelpText ?? "",
                                min_length = f.MinLength,
                                max_length = f.MaxLength,
                                default_value = f.DefaultValue ?? "",
                                options = f.Options != null
                                    ? f.Options.Select(o => new { value = o.Value, label = o.Label }).Cast<object>()
                                    : [],
                                foreign_key_info = f?.ForeignKeyInfo != null ? new
                                {
                                    referenced_table = f.ForeignKeyInfo.ReferencedTable ?? "",
                                    referenced_column = f.ForeignKeyInfo.ReferencedColumn ?? "",
                                    display_column = f.ForeignKeyInfo.DisplayColumn ?? "Not Set"
                                } : null,
                            }).ToList()
                        }).ToList()
                    }
                };
                string html, javascript;

                try
                {
                    // Try to use database templates
                    html = await _templateService.RenderTemplateByNameAsync("Bootstrap5_HTML", templateContext);
                    javascript = await _templateService.RenderTemplateByNameAsync("jQuery_JS", templateContext);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Database templates not found, using fallback templates");

                    // Use fallback inline templates
                    html = RenderFallbackHtmlTemplate(templateContext);
                    javascript = RenderFallbackJsTemplate(templateContext);
                }

                // Generate stored procedures if requested
                var storedProcedures = new List<GeneratedSpInfo>();
                if (request.GenerateStoredProcedures)
                {
                    storedProcedures.Add(new GeneratedSpInfo
                    {
                        SpName = $"usp_{tableName}_CUD",
                        SpType = "CUD",
                        Code = GenerateCrudStoredProcedure(config!),
                        FileName = $"usp_{tableName}_CUD.sql"
                    });

                    storedProcedures.Add(new GeneratedSpInfo
                    {
                        SpName = $"usp_{tableName}_Select",
                        SpType = "SELECT",
                        Code = GenerateSelectStoredProcedure(config!),
                        FileName = $"usp_{tableName}_Select.sql"
                    });
                }

                return new GenerateFormResponse
                {
                    Html = html,
                    JavaScript = javascript,
                    StoredProcedures = storedProcedures,
                    FileName = formName,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating form");
                throw;
            }
        }

        // ============================================
        // Fallback Templates (when DB templates not available)
        // ============================================
        private static string RenderFallbackHtmlTemplate(dynamic context)
        {
            var template = Scriban.Template.Parse(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{{ title }}</title>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
<body>
    <div class=""container py-5"">
        <div class=""card"">
            <div class=""card-header bg-primary text-white"">
                <h4 class=""mb-0"">{{ title }}</h4>
            </div>
            <div class=""card-body"">
                <form id=""{{ form_name }}"" class=""needs-validation"" novalidate>
                    <div class=""row g-3"">
                        {{~ for field in fields ~}}
                        <div class=""col-md-{{ field.col_size }}"">
                            <label for=""{{ field.column_name }}"" class=""form-label"">
                                {{ field.label }}
                                {{~ if field.required ~}}<span class=""text-danger"">*</span>{{~ end ~}}
                            </label>
                            {{~ if field.input_type == 'textarea' ~}}
                            <textarea id=""{{ field.column_name }}"" name=""{{ field.column_name }}"" 
                                      class=""form-control"" rows=""3"" 
                                      {{~ if field.required ~}}required{{~ end ~}}>{{ field.default_value }}</textarea>
                            {{~ else if field.input_type == 'select' ~}}
                            <select id=""{{ field.column_name }}"" name=""{{ field.column_name }}"" 
                                    class=""form-select"" {{~ if field.required ~}}required{{~ end ~}}>
                                <option value="""">Choose...</option>
                                {{~ for option in field.options ~}}
                                <option value=""{{ option.value }}"">{{ option.label }}</option>
                                {{~ end ~}}
                            </select>
                            {{~ else if field.input_type == 'checkbox' ~}}
                            <div class=""form-check"">
                                <input type=""checkbox"" id=""{{ field.column_name }}"" name=""{{ field.column_name }}"" 
                                       class=""form-check-input"" value=""true"">
                                <label class=""form-check-label"" for=""{{ field.column_name }}"">
                                    {{ field.label }}
                                </label>
                            </div>
                            {{~ else ~}}
                            <input type=""{{ field.input_type }}"" id=""{{ field.column_name }}"" name=""{{ field.column_name }}"" 
                                   class=""form-control"" value=""{{ field.default_value }}""
                                   {{~ if field.required ~}}required{{~ end ~}}
                                   {{~ if field.min_length ~}}minlength=""{{ field.min_length }}""{{~ end ~}}
                                   {{~ if field.max_length ~}}maxlength=""{{ field.max_length }}""{{~ end ~}}>
                            {{~ end ~}}
                            {{~ if field.help_text ~}}
                            <div class=""form-text"">{{ field.help_text }}</div>
                            {{~ end ~}}
                            <div class=""invalid-feedback"">This field is required.</div>
                        </div>
                        {{~ end ~}}
                    </div>
                    
                    <div class=""mt-4"">
                        <button type=""submit"" class=""btn btn-primary"">Save</button>
                        <button type=""button"" class=""btn btn-secondary ms-2"" onclick=""clearForm()"">Clear</button>
                    </div>
                </form>
            </div>
        </div>
    </div>
    
    <script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js""></script>
    <script src=""https://code.jquery.com/jquery-3.6.0.min.js""></script>
    <script src=""{{ form_name }}.js""></script>
</body>
</html>");

            return template.Render(context);
        }

        private static string RenderFallbackJsTemplate(dynamic context)
        {
            var template = Scriban.Template.Parse(@"// {{ form_name }}.js
$(document).ready(function() {
    // Form validation
    var forms = document.querySelectorAll('.needs-validation');
    Array.from(forms).forEach(function(form) {
        form.addEventListener('submit', function(event) {
            event.preventDefault();
            event.stopPropagation();
            
            if (form.checkValidity()) {
                saveData();
            }
            
            form.classList.add('was-validated');
        }, false);
    });
    
    function saveData() {
        var formData = {};
        {{~ for field in fields ~}}
        {{~ if field.input_type == 'checkbox' ~}}
        formData['{{ field.column_name }}'] = $('#{{ field.column_name }}').is(':checked');
        {{~ else ~}}
        formData['{{ field.column_name }}'] = $('#{{ field.column_name }}').val();
        {{~ end ~}}
        {{~ end ~}}
        
        $.ajax({
            url: '/api/{{ table_name }}/save',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(formData),
            success: function(response) {
                alert('Data saved successfully!');
                clearForm();
            },
            error: function(xhr) {
                alert('Error: ' + (xhr.responseJSON?.message || xhr.statusText));
            }
        });
    }
    
    window.clearForm = function() {
        $('#{{ form_name }}')[0].reset();
        $('#{{ form_name }}').removeClass('was-validated');
    };
});");

            return template.Render(context);
        }

        // ============================================
        // Stored Procedure Generation (simplified)
        // ============================================
        private static string GenerateCrudStoredProcedure(FormConfig config)
        {
            var fields = config.Groups?.SelectMany(g => g.Fields) ?? new List<FormField>();
            var tableName = config.TableName;
            var pkField = fields.FirstOrDefault(f => f.IsPrimaryKey);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"CREATE OR ALTER PROCEDURE [dbo].[usp_{tableName}_CUD]");
            sb.AppendLine("    @Action CHAR(1),");

            if (pkField != null)
            {
                sb.AppendLine($"    @{pkField.ColumnName} INT = NULL,");
            }

            foreach (var field in fields.Where(f => !f.IsIdentity))
            {
                var sqlType = MapToSqlType(field.DataType);
                var comma = field == fields.Where(f => !f.IsIdentity).Last() ? "" : ",";
                sb.AppendLine($"    @{field.ColumnName} {sqlType} = NULL{comma}");
            }

            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine();

            // INSERT
            sb.AppendLine("    IF @Action = 'C'");
            sb.AppendLine("    BEGIN");
            sb.AppendLine($"        INSERT INTO [{tableName}] (");
            var insertFields = fields.Where(f => !f.IsIdentity && f.IncludeInInsert).ToList();
            sb.AppendLine("            " + string.Join(", ", insertFields.Select(f => $"[{f.ColumnName}]")));
            sb.AppendLine("        ) VALUES (");
            sb.AppendLine("            " + string.Join(", ", insertFields.Select(f => $"@{f.ColumnName}")));
            sb.AppendLine("        );");
            sb.AppendLine("        SELECT SCOPE_IDENTITY() AS Id;");
            sb.AppendLine("    END");

            // UPDATE
            if (pkField != null)
            {
                sb.AppendLine();
                sb.AppendLine("    ELSE IF @Action = 'U'");
                sb.AppendLine("    BEGIN");
                sb.AppendLine($"        UPDATE [{tableName}] SET");
                var updateFields = fields.Where(f => !f.IsIdentity && !f.IsPrimaryKey && f.IncludeInUpdate).ToList();
                for (int i = 0; i < updateFields.Count; i++)
                {
                    var comma = i < updateFields.Count - 1 ? "," : "";
                    sb.AppendLine($"            [{updateFields[i].ColumnName}] = @{updateFields[i].ColumnName}{comma}");
                }
                sb.AppendLine($"        WHERE [{pkField.ColumnName}] = @{pkField.ColumnName};");
                sb.AppendLine("    END");

                // DELETE
                sb.AppendLine();
                sb.AppendLine("    ELSE IF @Action = 'D'");
                sb.AppendLine("    BEGIN");
                sb.AppendLine($"        DELETE FROM [{tableName}] WHERE [{pkField.ColumnName}] = @{pkField.ColumnName};");
                sb.AppendLine("    END");
            }

            sb.AppendLine("END");
            return sb.ToString();
        }

        private static string GenerateSelectStoredProcedure(FormConfig config)
        {
            var tableName = config.TableName;
            var pkField = config.Groups?.SelectMany(g => g.Fields).FirstOrDefault(f => f.IsPrimaryKey);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"CREATE OR ALTER PROCEDURE [dbo].[usp_{tableName}_Select]");
            if (pkField != null)
            {
                sb.AppendLine($"    @{pkField.ColumnName} INT = NULL");
            }
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine();
            sb.AppendLine($"    SELECT * FROM [{tableName}]");
            if (pkField != null)
            {
                sb.AppendLine($"    WHERE (@{pkField.ColumnName} IS NULL OR [{pkField.ColumnName}] = @{pkField.ColumnName});");
            }
            sb.AppendLine("END");

            return sb.ToString();
        }

        private static string MapToSqlType(string dataType)
        {
            return dataType?.ToUpper() switch
            {
                "INT" => "INT",
                "BIGINT" => "BIGINT",
                "VARCHAR" => "NVARCHAR(255)",
                "NVARCHAR" => "NVARCHAR(255)",
                "DATE" => "DATE",
                "DATETIME" => "DATETIME2",
                "BIT" => "BIT",
                "DECIMAL" => "DECIMAL(18,2)",
                "TEXT" => "NVARCHAR(MAX)",
                _ => "NVARCHAR(255)"
            };
        }

        private async Task LogGenerationHistory(FormConfig config, string generationType)
        {
            try
            {
                if (string.IsNullOrEmpty(config.Id))
                {
                    _logger.LogWarning("Cannot log generation history: FormConfig.Id is null or empty");
                    return;
                }

                var history = new GenerationHistory
                {
                    FormConfigId = config.Id,
                    GenerationType = generationType,
                    HtmlGenerated = true,
                    JavaScriptGenerated = true,
                    SpGenerated = true,
                    FieldCount = config.Groups?.Sum(g => g.Fields.Count) ?? 0,
                    GroupCount = config.Groups?.Count ?? 0
                };

                await _generationHistoryRepo.SaveAsync(history);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log generation history");
            }
        }
    }
}