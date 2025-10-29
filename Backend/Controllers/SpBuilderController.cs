using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.SpBuilder;
using ActoEngine.WebApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpBuilderController : ControllerBase
{
    private readonly ISpBuilderService _spBuilder;
    private readonly ISchemaSyncRepository _schemaRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly ILogger<SpBuilderController> _log;

    public SpBuilderController(
        ISpBuilderService spBuilder,
        ISchemaSyncRepository schemaRepo,
        IProjectRepository projectRepo,
        ILogger<SpBuilderController> log)
    {
        _spBuilder = spBuilder;
        _schemaRepo = schemaRepo;
        _projectRepo = projectRepo;
        _log = log;
    }

    /// <summary>
    /// Generate CUD or SELECT stored procedure
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ApiResponse<GeneratedSpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<GeneratedSpResponse>>> Generate(
        [FromBody] SpGenerationRequest req)
    {
        try
        {
            var result = await _spBuilder.GenerateStoredProcedure(req);

            if (result == null)
            {
                return StatusCode(500, ApiResponse<string>.Failure(
                    "SP generation failed", ["No result returned from code generator."]));
            }

            var spName = result.StoredProcedure?.SpName ?? "Unknown";
            return Ok(ApiResponse<GeneratedSpResponse>.Success(
                result,
                $"Generated {req.Type} SP: {spName}"));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed generating SP: {Table}", req.TableName);
            return StatusCode(500, ApiResponse<GeneratedSpResponse>.Failure(
                "SP generation failed", ["An error occurred while generating the stored procedure. Please check the logs for details."]));
        }
    }

    /// <summary>
    /// Get table schema
    /// </summary>
    [HttpPost("schema/table")]
    [ProducesResponseType(typeof(ApiResponse<TableSchemaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<TableSchemaResponse>>> GetSchema(
        [FromBody] TableSchemaRequest req)
    {
        try
        {
            var result = await _spBuilder.GetTableSchema(req);
            return Ok(ApiResponse<TableSchemaResponse>.Success(
                result,
                $"Found {result.Columns.Count} columns"));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed reading schema: {Table}", req.TableName);
            return StatusCode(500, ApiResponse<TableSchemaResponse>.Failure(
                "Schema read failed", ["An error occurred while reading the table schema. Please check the logs for details."]));
        }
    }

    /// <summary>
    /// Get all tables from project
    /// </summary>
    [HttpGet("schema/tables/{projectId}")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetTables(int projectId)
    {
        try
        {
            var project = await _projectRepo.GetByIdInternalAsync(projectId) ?? throw new InvalidOperationException($"Project with ID {projectId} not found.");
            var tables = await _schemaRepo.GetAllTablesAsync(project.ConnectionString);

            return Ok(ApiResponse<List<string>>.Success(
                tables,
                $"Found {tables.Count} tables"));
        }
        catch (InvalidOperationException ex)
        {
            _log.LogWarning(ex, "Project not found: {ProjectId}", projectId);
            return NotFound(ApiResponse<List<string>>.Failure(
                "Project not found", ["The specified project does not exist."]));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed getting tables: {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<string>>.Failure(
                "Tables fetch failed", ["An error occurred while fetching the tables. Please check the logs for details."]));
        }
    }

    /// <summary>
    /// Quick CUD generation - auto-read schema
    /// </summary>
    [HttpPost("quick/cud")]
    public async Task<ActionResult<ApiResponse<GeneratedSpResponse>>> QuickCud(
        [FromBody] QuickGenerateRequest req)
    {
        try
        {
            _log.LogInformation("Quick CUD for: {Table}", req.TableName);

            var schema = await _spBuilder.GetTableSchema(new TableSchemaRequest
            {
                ProjectId = req.ProjectId,
                TableName = req.TableName
            });

            var cols = MapSchemaToColumns(schema);

            var result = await _spBuilder.GenerateStoredProcedure(new SpGenerationRequest
            {
                ProjectId = req.ProjectId,
                TableName = req.TableName,
                Type = SpType.Cud,
                Columns = cols,
                CudOptions = req.CudOptions ?? new CudSpOptions(),
                SelectOptions = new SelectSpOptions() // Required but not used for CUD
            });

            return Ok(ApiResponse<GeneratedSpResponse>.Success(
                result,
                $"Generated CUD SP: {result.StoredProcedure?.SpName ?? "Unknown"}"));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Quick CUD failed: {Table}", req.TableName);
            return StatusCode(500, ApiResponse<GeneratedSpResponse>.Failure(
                "Quick CUD generation failed", ["An error occurred while generating the CUD stored procedure. Please check the logs for details."]));
        }
    }

    /// <summary>
    /// Quick SELECT generation - auto-read schema
    /// </summary>
    [HttpPost("quick/select")]
    public async Task<ActionResult<ApiResponse<GeneratedSpResponse>>> QuickSelect(
        [FromBody] QuickGenerateRequest req)
    {
        try
        {
            _log.LogInformation("Quick SELECT for: {Table}", req.TableName);

            var schema = await _spBuilder.GetTableSchema(new TableSchemaRequest
            {
                ProjectId = req.ProjectId,
                TableName = req.TableName
            });

            var cols = MapSchemaToColumns(schema);

            var result = await _spBuilder.GenerateStoredProcedure(new SpGenerationRequest
            {
                ProjectId = req.ProjectId,
                TableName = req.TableName,
                Type = SpType.Select,
                Columns = cols,
                CudOptions = new CudSpOptions(), // Required but not used for SELECT
                SelectOptions = req.SelectOptions ?? new SelectSpOptions()
            });

            return Ok(ApiResponse<GeneratedSpResponse>.Success(
                result,
                $"Generated SELECT SP: {result.StoredProcedure?.SpName ?? "Unknown"}"));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Quick SELECT failed: {Table}", req.TableName);
            return StatusCode(500, ApiResponse<GeneratedSpResponse>.Failure(
                "Quick SELECT generation failed", ["An error occurred while generating the SELECT stored procedure. Please check the logs for details."]));
        }
    }

    // Helper
    private static List<SpColumnConfig> MapSchemaToColumns(TableSchemaResponse schema)
    {
        return [.. schema.Columns.Select(c => new SpColumnConfig
        {
            ColumnName = c.ColumnName,
            DataType = c.DataType,
            MaxLength = c.MaxLength,
            Precision = c.Precision,
            Scale = c.Scale,
            IsNullable = c.IsNullable,
            IsPrimaryKey = c.IsPrimaryKey,
            IsIdentity = c.IsIdentity,
            IncludeInCreate = !c.IsIdentity,
            IncludeInUpdate = !c.IsIdentity && !c.IsPrimaryKey,
            DefaultValue = c.DefaultValue
        })];
    }
}