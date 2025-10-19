using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Services.FormBuilderService;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using Dapper;

namespace ActoEngine.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FormBuilderController : ControllerBase
    {
        private readonly IFormBuilderService _formBuilderService;
        private readonly ILogger<FormBuilderController> _logger;

        public FormBuilderController(
            IFormBuilderService formBuilderService,
            ILogger<FormBuilderController> logger)
        {
            _formBuilderService = formBuilderService;
            _logger = logger;
        }

        /// <summary>
        /// Save form configuration
        /// </summary>
        [HttpPost("save")]
        public async Task<IActionResult> SaveFormConfig([FromBody] SaveFormConfigRequest request)
        {
            try
            {
                if (request?.Config == null)
                {
                    return BadRequest(new { success = false, message = "Invalid form configuration" });
                }

                var userId = int.Parse(User.FindFirst("user_id")?.Value ?? "1");
                var result = await _formBuilderService.SaveFormConfigAsync(request);
                return Ok(ApiResponse<SaveFormConfigResponse>.Success(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving form configuration");
                return StatusCode(500, ApiResponse<GenerateFormResponse>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Load form configuration by ID or name
        /// </summary>
        [HttpGet("load/{formId}")]
        public async Task<IActionResult> LoadFormConfig(string formId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("user_id")?.Value ?? "1");
                var config = await _formBuilderService.LoadFormConfigAsync(new LoadFormConfigRequest { FormId = formId });
                
                return Ok(ApiResponse<FormConfig>.Success(config));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading form configuration");
                return NotFound(ApiResponse<FormConfig>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Get all form configurations for a project
        /// </summary>
        [HttpGet("configs/{projectId}")]
        public async Task<IActionResult> GetFormConfigs(int projectId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("user_id")?.Value ?? "1");
                var configs = await _formBuilderService.GetFormConfigsAsync(projectId);
                return Ok(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting form configurations");
                return StatusCode(500, ApiResponse<GenerateFormResponse>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Generate HTML, JavaScript, and optionally stored procedures
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateForm([FromBody] GenerateFormRequest request)
        {
            try
            {
                if (request?.Config == null)
                {
                    return BadRequest(new { success = false, message = "Invalid form configuration" });
                }

                var userId = int.Parse(User.FindFirst("user_id")?.Value ?? "1");
                var result = await _formBuilderService.GenerateFormAsync(request);
                return Ok(ApiResponse<GenerateFormResponse>.Success(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating form");
                return StatusCode(500, ApiResponse<GenerateFormResponse>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Delete a form configuration
        /// </summary>
        [HttpDelete("delete/{formId}")]
        public async Task<IActionResult> DeleteFormConfig(int formId)
        {
            try
            {
                using var connection = HttpContext.RequestServices.GetRequiredService<IDbConnectionFactory>().CreateConnection();
                
                var sql = "DELETE FROM FormConfigs WHERE Id = @Id";
                await connection.ExecuteAsync(sql, new { Id = formId });
                
                return Ok(ApiResponse<string>.Success("Form configuration deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting form configuration");
                return StatusCode(500, ApiResponse<string>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Get available templates
        /// </summary>
        [HttpGet("templates")]
        public async Task<IActionResult> GetTemplates()
        {
            try
            {
                using var connection = HttpContext.RequestServices.GetRequiredService<IDbConnectionFactory>().CreateConnection();
                
                var sql = @"
                    SELECT Id, TemplateName, TemplateType, Framework, Version, Description, IsActive
                    FROM CodeTemplates
                    WHERE IsActive = 1
                    ORDER BY TemplateType, TemplateName";
                
                var templates = await connection.QueryAsync<dynamic>(sql);
                return Ok(ApiResponse<IEnumerable<dynamic>>.Success(templates));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting templates");
                return StatusCode(500, ApiResponse<string>.Failure(ex.Message));
            }
        }
    }
}