using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Services.FormBuilderService;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Extensions;

namespace ActoEngine.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FormBuilderController(
        IFormBuilderService formBuilderService,
        ILogger<FormBuilderController> logger) : ControllerBase
    {
        private readonly IFormBuilderService _formBuilderService = formBuilderService;
        private readonly ILogger<FormBuilderController> _logger = logger;

        /// <summary>
        /// Save form configuration
        /// </summary>
        [HttpPost("save")]
        [ProducesResponseType(typeof(ApiResponse<SaveFormConfigResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SaveFormConfig([FromBody] SaveFormConfigRequest request)
        {
            try
            {
                if (request?.Config == null)
                {
                    return BadRequest(ApiResponse<object>.Failure("Invalid form configuration"));
                }
                var result = await _formBuilderService.SaveFormConfigAsync(request);
                return Ok(ApiResponse<SaveFormConfigResponse>.Success(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving form configuration");
                return StatusCode(500, ApiResponse<SaveFormConfigResponse>.Failure("Failed to save form configuration", [ex.Message]));
            }
        }

        /// <summary>
        /// Load form configuration by ID or name
        /// </summary>
        [HttpGet("load/{formId}")]
        [ProducesResponseType(typeof(ApiResponse<FormConfig>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LoadFormConfig(string formId)
        {
            try
            {
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
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<FormConfigListItem>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<FormConfigListItem>>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFormConfigs(int projectId)
        {
            try
            {
                var configs = await _formBuilderService.GetFormConfigsAsync(projectId);
                return Ok(ApiResponse<IEnumerable<FormConfigListItem>>.Success(configs, "Form configurations retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting form configurations");
                return StatusCode(500, ApiResponse<IEnumerable<FormConfigListItem>>.Failure("Failed to get form configurations", [ex.Message]));
            }
        }

        /// <summary>
        /// Generate HTML, JavaScript, and optionally stored procedures
        /// </summary>
        [HttpPost("generate")]
        [ProducesResponseType(typeof(ApiResponse<GenerateFormResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateForm([FromBody] GenerateFormRequest request)
        {
            try
            {
                if (request?.Config == null)
                {
                    return BadRequest(ApiResponse<object>.Failure("Invalid form configuration"));
                }
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
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteFormConfig(int formId)
        {
            try
            {
                var userId = HttpContext.GetUserId();
                if (userId == null)
                    return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

                var success = await _formBuilderService.DeleteFormConfigAsync(formId.ToString(), userId.Value);
                if (!success)
                    return NotFound(ApiResponse<string>.Failure("Form configuration not found or could not be deleted"));
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
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTemplates()
        {
            try
            {
                var templates = await _formBuilderService.GetTemplatesAsync();
                return Ok(ApiResponse<IEnumerable<object>>.Success(templates));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting templates");
                return StatusCode(500, ApiResponse<string>.Failure(ex.Message));
            }
        }
    }
}