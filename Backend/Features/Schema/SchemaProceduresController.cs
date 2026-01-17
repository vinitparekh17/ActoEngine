using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Features.Projects;

namespace ActoEngine.WebApi.Features.Schema;

[ApiController]
[Authorize]
[Route("api/schema")]
public class SchemaProceduresController(
    SchemaService schemaService,
    ProjectRepository projectRepository,
    ILogger<SchemaProceduresController> logger) : ControllerBase
{
    private readonly SchemaService _schemaService = schemaService;
    private readonly ProjectRepository _projectRepository = projectRepository;
    private readonly ILogger<SchemaProceduresController> _logger = logger;

    /// <summary>
    /// Get all stored procedures for a project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>List of stored procedures</returns>
    [HttpGet("projects/{projectId}/stored-procedures")]
    [ProducesResponseType(typeof(ApiResponse<List<StoredProcedureMetadataDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<StoredProcedureMetadataDto>>> GetStoredProcedures(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting stored procedures for project {ProjectId}", projectId);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            var procedures = await _schemaService.GetStoredProceduresMetadataAsync(projectId);
            // If you want to return the same DTO as before, you may need to map it
            return Ok(ApiResponse<List<StoredProcedureMetadataDto>>.Success(procedures));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored procedures for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<StoredProcedureMetadataDto>>.Failure("An error occurred while retrieving stored procedures"));
        }
    }

    /// <summary>
    /// Get stored procedures list for a project (lightweight - minimal bandwidth)
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>Lightweight list of stored procedures with minimal fields</returns>
    [HttpGet("projects/{projectId}/stored-procedures-metadata")]
    [ProducesResponseType(typeof(ApiResponse<List<StoredProcedureListDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<StoredProcedureListDto>>> GetStoredProceduresMetadata(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting stored procedures list for project {ProjectId}", projectId);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound(ApiResponse<List<StoredProcedureListDto>>.Failure($"Project with ID {projectId} not found"));
            }

            var procedures = await _schemaService.GetStoredProceduresListAsync(projectId);
            return Ok(ApiResponse<List<StoredProcedureListDto>>.Success(procedures));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored procedures list for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<StoredProcedureListDto>>.Failure("An error occurred while retrieving stored procedures list"));
        }
    }

    /// <summary>
    /// Get stored procedure metadata for a project from ActoEngine database (legacy endpoint)
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>List of stored procedure metadata</returns>
    [HttpGet("projects/{projectId}/sp-metadata")]
    [ProducesResponseType(typeof(ApiResponse<List<StoredProcedureListDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    [Obsolete("Use /stored-procedures-metadata instead")]
    public async Task<ActionResult<List<StoredProcedureListDto>>> GetSPMetadata(int projectId)
    {
        // Redirect to the new endpoint
        return await GetStoredProceduresMetadata(projectId);
    }

    /// <summary>
    /// Get detailed stored procedure information by procedure ID
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="procedureId">The stored procedure ID</param>
    /// <returns>Detailed stored procedure information</returns>
    [HttpGet("projects/{projectId}/stored-procedures/{procedureId}")]
    [ProducesResponseType(typeof(ApiResponse<StoredProcedureDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StoredProcedureDetailResponse>> GetStoredProcedureDetail(int projectId, int procedureId)
    {
        try
        {
            _logger.LogInformation("Getting stored procedure details for procedure {ProcedureId} in project {ProjectId}", procedureId, projectId);

            var procedure = await _schemaService.GetSpByIdAsync(procedureId);
            if (procedure == null)
            {
                return NotFound(ApiResponse<StoredProcedureDetailResponse>.Failure($"Stored procedure with ID {procedureId} not found"));
            }

            // Verify procedure belongs to the requested project
            if (procedure.ProjectId != projectId)
            {
                _logger.LogWarning("Stored procedure {ProcedureId} belongs to project {ActualProjectId} but was requested for project {RequestedProjectId}",
                    procedureId, procedure.ProjectId, projectId);
                return NotFound(ApiResponse<StoredProcedureDetailResponse>.Failure($"Stored procedure with ID {procedureId} not found"));
            }

            // Build the detailed response
            var response = new StoredProcedureDetailResponse
            {
                StoredProcedureId = procedure.SpId,
                ProcedureName = procedure.ProcedureName,
                SchemaName = procedure.SchemaName ?? "dbo",
                Definition = procedure.Definition,
                Parameters = null, // Can be populated from parameter metadata if available
                CreatedDate = procedure.CreatedAt,
                ModifiedDate = procedure.UpdatedAt,
                Description = procedure.Description
            };

            return Ok(ApiResponse<StoredProcedureDetailResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored procedure details for procedure {ProcedureId} in project {ProjectId}", procedureId, projectId);
            return StatusCode(500, ApiResponse<StoredProcedureDetailResponse>.Failure("An error occurred while retrieving stored procedure details"));
        }
    }
}

