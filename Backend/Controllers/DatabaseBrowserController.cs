using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Schema;
using ActoEngine.WebApi.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace ActoEngine.Api.Controllers;

[ApiController]
[Authorize]
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
    /// Get database tree structure for frontend display
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>Tree structure with database, tables, columns, stored procedures, etc.</returns>
    [HttpGet("projects/{projectId}/tree")]
    public async Task<ActionResult<TreeNode>> GetDatabaseTree(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting database tree for project {ProjectId}", projectId);
            
            var project = await _projectRepository.GetByIdInternalAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            // Use project name or database name from connection string
            var databaseName = project.ProjectName ?? "Database";
            
            var tree = await _schemaService.GetDatabaseTreeAsync(projectId, databaseName);
            return Ok(ApiResponse<TreeNode>.Success(tree));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database tree for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<TreeNode>.Failure("An error occurred while retrieving database tree"));
        }
    }

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
            
            var project = await _projectRepository.GetByIdInternalAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.ConnectionString))
            {
                return BadRequest("Project connection string is not configured");
            }

            var tables = await _schemaService.GetStoredTablesAsync(projectId);
            // Return just the table names
            var tableNames = tables.Select(t => t.TableName).ToList();
            return Ok(ApiResponse<List<string>>.Success(tableNames));
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
            
            var project = await _projectRepository.GetByIdInternalAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.ConnectionString))
            {
                return BadRequest("Project connection string is not configured");
            }

            // Use stored metadata to build structure
            var tables = await _schemaService.GetStoredTablesAsync(projectId);
            var structure = tables.Select(t => new DatabaseTableInfo
            {
                TableName = t.TableName,
                SchemaName = GetSchemaNameWithFallback(t.SchemaName, project.DatabaseType),
                Description = t.Description,
                // Add more fields as needed from metadata
            }).ToList();
            return Ok(ApiResponse<List<DatabaseTableInfo>>.Success(structure));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database structure for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<DatabaseTableInfo>>.Failure("An error occurred while retrieving database structure"));
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

            var project = await _projectRepository.GetByIdInternalAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.ConnectionString))
            {
                return BadRequest("Project connection string is not configured");
            }

            var schema = await _schemaService.GetStoredTableSchemaAsync(projectId, tableName);
            return Ok(ApiResponse<TableSchemaResponse>.Success(schema));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schema for table {TableName} in project {ProjectId}", tableName, projectId);
            return StatusCode(500, ApiResponse<TableSchemaResponse>.Failure("An error occurred while retrieving table schema"));
        }
    }

    /// <summary>
    /// Get all stored procedures for a project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>List of stored procedures</returns>
    [HttpGet("projects/{projectId}/stored-procedures")]
    public async Task<ActionResult<List<StoredProcedureMetadata>>> GetStoredProcedures(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting stored procedures for project {ProjectId}", projectId);
            
            var project = await _projectRepository.GetByIdInternalAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.ConnectionString))
            {
                return BadRequest("Project connection string is not configured");
            }

            var procedures = await _schemaService.GetStoredProceduresMetadataAsync(projectId);
            // If you want to return the same DTO as before, you may need to map it
            return Ok(ApiResponse<List<StoredProcedureMetadataDto>>.Success(procedures));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored procedures for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<StoredProcedureMetadata>>.Failure("An error occurred while retrieving stored procedures"));
        }
    }

    // ===== STORED METADATA ENDPOINTS =====
    // These endpoints retrieve the stored metadata from ActoEngine database
    // instead of querying the target database directly

    /// <summary>
    /// Get stored tables metadata for a project from ActoEngine database
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>List of stored table metadata</returns>
    [HttpGet("projects/{projectId}/stored-tables")]
    public async Task<ActionResult<List<TableMetadataDto>>> GetStoredTables(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting stored tables metadata for project {ProjectId}", projectId);
            
            var project = await _projectRepository.GetByIdInternalAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            var tables = await _schemaService.GetStoredTablesAsync(projectId);
            return Ok(tables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored tables metadata for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<TableMetadataDto>>.Failure("An error occurred while retrieving stored tables metadata"));
        }
    }

    /// <summary>
    /// Get stored columns metadata for a table from ActoEngine database
    /// </summary>
    /// <param name="tableId">The table ID</param>
    /// <returns>List of stored column metadata</returns>
    [HttpGet("tables/{tableId}/stored-columns")]
    public async Task<ActionResult<List<ColumnMetadataDto>>> GetStoredColumns(int tableId)
    {
        try
        {
            _logger.LogInformation("Getting stored columns metadata for table {TableId}", tableId);
            
            var columns = await _schemaService.GetStoredColumnsAsync(tableId);
            return Ok(ApiResponse<List<ColumnMetadataDto>>.Success(columns));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored columns metadata for table {TableId}", tableId);
            return StatusCode(500, ApiResponse<List<ColumnMetadataDto>>.Failure("An error occurred while retrieving stored columns metadata"));
        }
    }

    /// <summary>
    /// Get stored procedure metadata for a project from ActoEngine database
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>List of stored procedure metadata</returns>
    [HttpGet("projects/{projectId}/stored-procedures-metadata")]
    public async Task<ActionResult<List<StoredProcedureMetadataDto>>> GetStoredProceduresMetadata(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting stored procedure metadata for project {ProjectId}", projectId);
            
            var project = await _projectRepository.GetByIdInternalAsync(projectId);
            if (project == null)
            {
                return NotFound(ApiResponse<List<StoredProcedureMetadataDto>>.Failure($"Project with ID {projectId} not found"));
            }

            var procedures = await _schemaService.GetStoredProceduresMetadataAsync(projectId);
            return Ok(ApiResponse<List<StoredProcedureMetadataDto>>.Success(procedures));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored procedure metadata for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<StoredProcedureMetadataDto>>.Failure("An error occurred while retrieving stored procedure metadata"));
        }
    }

    /// <summary>
    /// Get stored table schema for a specific table from ActoEngine database
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="tableName">The table name</param>
    /// <returns>Stored table schema with columns and metadata</returns>
    [HttpGet("projects/{projectId}/stored-tables/{tableName}/schema")]
    public async Task<ActionResult<TableSchemaResponse>> GetStoredTableSchema(int projectId, string tableName)
    {
        try
        {
            _logger.LogInformation("Getting stored table schema for table {TableName} in project {ProjectId}", tableName, projectId);
            
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return BadRequest("Table name cannot be empty");
            }

            var project = await _projectRepository.GetByIdInternalAsync(projectId);
            if (project == null)
            {
                return NotFound(ApiResponse<TableSchemaResponse>.Failure($"Project with ID {projectId} not found"));
            }

            var schema = await _schemaService.GetStoredTableSchemaAsync(projectId, tableName);
            return Ok(ApiResponse<TableSchemaResponse>.Success(schema));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored table schema for table {TableName} in project {ProjectId}", tableName, projectId);
            return StatusCode(500, ApiResponse<TableSchemaResponse>.Failure("An error occurred while retrieving stored table schema"));
        }
    }

    /// <summary>
    /// Get data from a specific table in a project's database
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="tableName">The table name</param>
    /// <param name="limit">Maximum number of rows to return (default: 100)</param>
    /// <returns>List of rows from the table</returns>
    [HttpGet("projects/{projectId}/tables/{tableName}/columns")]
    public async Task<ActionResult<List<string>>> GetTableColumns(
        int projectId, 
        string tableName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return BadRequest(ApiResponse<List<Dictionary<string, object>>>.Failure("Table name cannot be empty"));
            }

            // Validate project exists and get connection string
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound(ApiResponse<List<Dictionary<string, object>>>.Failure($"Project with ID {projectId} not found"));
            }

            // Get actual data from the database using the connection string
            var data = await _schemaService.GetStoredTableSchemaAsync(project.ProjectId, tableName);
            var columnList = data.Columns.Select(c => c.ColumnName).ToList();
            return Ok(ApiResponse<List<string>>.Success(columnList));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Table {TableName} not found in project {ProjectId}", tableName, projectId);
            return NotFound(ApiResponse<List<string>>.Failure(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for table data from {TableName} in project {ProjectId}", tableName, projectId);
            return BadRequest(ApiResponse<List<string>>.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data from table {TableName} for project {ProjectId}", tableName, projectId);
            return StatusCode(500, ApiResponse<List<Dictionary<string, object?>>>.Failure("An error occurred while retrieving table data"));
        }
    }

    /// <summary>
    /// Get schema name with provider-specific fallback
    /// </summary>
    /// <param name="schemaName">The schema name from metadata (may be null)</param>
    /// <param name="databaseType">The database type (SqlServer, PostgreSQL, MySQL, etc.)</param>
    /// <returns>Schema name or provider-specific default</returns>
    private static string GetSchemaNameWithFallback(string? schemaName, string? databaseType)
    {
        // If schema name is provided and not empty, use it
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            return schemaName;
        }

        // Otherwise, return provider-specific default
        return (databaseType?.ToLower()) switch
        {
            "postgresql" or "postgres" => "public",
            "mysql" => "", // MySQL doesn't use schemas in the same way
            "sqlserver" or "mssql" => "dbo",
            "oracle" => "SYSTEM", // Common default, but often user-specific
            _ => "dbo" // Default fallback to SQL Server convention
        };
    }
}
