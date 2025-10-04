using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Schema;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseBrowserController(
    ISchemaService schemaService,
    IProjectRepository projectRepository,
    ILogger<DatabaseBrowserController> logger) : ControllerBase
{
    private readonly ISchemaService _schemaService = schemaService;
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly ILogger<DatabaseBrowserController> _logger = logger;

    /// <summary>
    /// Get all tables in the database for a project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>List of table names</returns>
    [HttpGet("projects/{projectId}/tables")]
    public async Task<ActionResult<List<string>>> GetAllTables(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting all tables for project {ProjectId}", projectId);
            
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.ConnectionString))
            {
                return BadRequest("Project connection string is not configured");
            }

            var tables = await _schemaService.GetAllTablesAsync(project.ConnectionString);
            return Ok(tables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tables for project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving tables");
        }
    }

    /// <summary>
    /// Get database structure (schemas and tables) for a project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>Database structure with schemas and tables</returns>
    [HttpGet("projects/{projectId}/structure")]
    public async Task<ActionResult<List<DatabaseTableInfo>>> GetDatabaseStructure(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting database structure for project {ProjectId}", projectId);
            
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.ConnectionString))
            {
                return BadRequest("Project connection string is not configured");
            }

            var structure = await _schemaService.GetDatabaseStructureAsync(project.ConnectionString);
            return Ok(structure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database structure for project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving database structure");
        }
    }

    /// <summary>
    /// Get detailed schema information for a specific table
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="tableName">The table name</param>
    /// <returns>Table schema with columns and metadata</returns>
    [HttpGet("projects/{projectId}/tables/{tableName}/schema")]
    public async Task<ActionResult<TableSchemaResponse>> GetTableSchema(int projectId, string tableName)
    {
        try
        {
            _logger.LogInformation("Getting schema for table {TableName} in project {ProjectId}", tableName, projectId);
            
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return BadRequest("Table name cannot be empty");
            }

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.ConnectionString))
            {
                return BadRequest("Project connection string is not configured");
            }

            var schema = await _schemaService.GetTableSchemaAsync(project.ConnectionString, tableName);
            return Ok(schema);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schema for table {TableName} in project {ProjectId}", tableName, projectId);
            return StatusCode(500, "An error occurred while retrieving table schema");
        }
    }

    /// <summary>
    /// Get table schema using connection string directly (for testing/development)
    /// </summary>
    /// <param name="request">Table schema request with connection details</param>
    /// <returns>Table schema with columns and metadata</returns>
    [HttpPost("table-schema")]
    public async Task<ActionResult<TableSchemaResponse>> GetTableSchemaByConnectionString([FromBody] TableSchemaDirectRequest request)
    {
        try
        {
            _logger.LogInformation("Getting schema for table {TableName} using direct connection", request.TableName);
            
            if (string.IsNullOrWhiteSpace(request.TableName))
            {
                return BadRequest("Table name cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(request.ConnectionString))
            {
                return BadRequest("Connection string cannot be empty");
            }

            var schema = await _schemaService.GetTableSchemaAsync(request.ConnectionString, request.TableName);
            return Ok(schema);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schema for table {TableName}", request.TableName);
            return StatusCode(500, "An error occurred while retrieving table schema");
        }
    }

    /// <summary>
    /// Get all tables using connection string directly (for testing/development)
    /// </summary>
    /// <param name="request">Direct connection request</param>
    /// <returns>List of table names</returns>
    [HttpPost("tables")]
    public async Task<ActionResult<List<string>>> GetAllTablesByConnectionString([FromBody] DirectConnectionRequest request)
    {
        try
        {
            _logger.LogInformation("Getting all tables using direct connection");
            
            if (string.IsNullOrWhiteSpace(request.ConnectionString))
            {
                return BadRequest("Connection string cannot be empty");
            }

            var tables = await _schemaService.GetAllTablesAsync(request.ConnectionString);
            return Ok(tables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tables using direct connection");
            return StatusCode(500, "An error occurred while retrieving tables");
        }
    }
}

/// <summary>
/// Request model for getting table schema with direct connection string
/// </summary>
public class TableSchemaDirectRequest
{
    public required string ConnectionString { get; set; }
    public required string TableName { get; set; }
}

/// <summary>
/// Request model for direct database connection operations
/// </summary>
public class DirectConnectionRequest
{
    public required string ConnectionString { get; set; }
}