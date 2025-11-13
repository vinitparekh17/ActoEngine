using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Schema;
using ActoEngine.WebApi.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace ActoEngine.WebApi.Controllers;

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
    [ProducesResponseType(typeof(ApiResponse<TreeNode>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TreeNode>> GetDatabaseTree(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting database tree for project {ProjectId}", projectId);

            var project = await _projectRepository.GetByIdAsync(projectId);
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
    /// Get database structure (schemas and tables) for a project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>Database structure with schemas and tables</returns>
    [HttpGet("projects/{projectId}/structure")]
    [ProducesResponseType(typeof(ApiResponse<List<DatabaseTableInfo>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(ApiResponse<TableSchemaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
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

            if (!project.IsLinked)
            {
                return BadRequest("Project is not linked to a database. Please link the project first.");
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

    // ===== STORED METADATA ENDPOINTS =====
    // These endpoints retrieve the stored metadata from ActoEngine database
    // instead of querying the target database directly

    /// <summary>
    /// Get stored tables list for a project (lightweight - minimal bandwidth)
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>Lightweight list of tables with minimal fields</returns>
    [HttpGet("projects/{projectId}/tables")]
    [ProducesResponseType(typeof(ApiResponse<List<TableListDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<TableListDto>>> GetStoredTables(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting tables list for project {ProjectId}", projectId);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            var tables = await _schemaService.GetTablesListAsync(projectId);
            return Ok(ApiResponse<List<TableListDto>>.Success(tables));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tables list for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<TableListDto>>.Failure("An error occurred while retrieving tables list"));
        }
    }

    /// <summary>
    /// Get stored columns metadata for a table from ActoEngine database
    /// </summary>
    /// <param name="tableId">The table ID</param>
    /// <returns>List of stored column metadata</returns>
    [HttpGet("tables/{tableId}/stored-columns")]
    [ProducesResponseType(typeof(ApiResponse<List<ColumnMetadataDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
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
    /// Get stored table schema for a specific table from ActoEngine database
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="tableName">The table name</param>
    /// <returns>Stored table schema with columns and metadata</returns>
    [HttpGet("projects/{projectId}/stored-tables/{tableName}/schema")]
    [ProducesResponseType(typeof(ApiResponse<TableSchemaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TableSchemaResponse>> GetStoredTableSchema(int projectId, string tableName)
    {
        try
        {
            _logger.LogInformation("Getting stored table schema for table {TableName} in project {ProjectId}", tableName, projectId);

            if (string.IsNullOrWhiteSpace(tableName))
            {
                return BadRequest("Table name cannot be empty");
            }

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound(ApiResponse<TableSchemaResponse>.Failure($"Project with ID {projectId} not found"));
            }

            if (!project.IsLinked)
            {
                return BadRequest(ApiResponse<TableSchemaResponse>.Failure("Project is not linked to a database. Please link the project first."));
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
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
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
    
    // ===== ENTITY DETAIL ENDPOINTS =====
    // These endpoints retrieve detailed information about specific database entities by ID

    /// <summary>
    /// Get detailed table information by table ID
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="tableId">The table ID</param>
    /// <returns>Detailed table information including columns, keys, and indexes</returns>
    [HttpGet("projects/{projectId}/tables/{tableId}")]
    [ProducesResponseType(typeof(ApiResponse<TableDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TableDetailResponse>> GetTableDetail(int projectId, int tableId)
    {
        try
        {
            _logger.LogInformation("Getting table details for table {TableId} in project {ProjectId}", tableId, projectId);

            // Get basic table metadata
            var table = await _schemaService.GetTableByIdAsync(tableId);
            if (table == null)
            {
                return NotFound(ApiResponse<TableDetailResponse>.Failure($"Table with ID {tableId} not found"));
            }

            // Verify table belongs to the requested project
            if (table.ProjectId != projectId)
            {
                _logger.LogWarning("Table {TableId} belongs to project {ActualProjectId} but was requested for project {RequestedProjectId}",
                    tableId, table.ProjectId, projectId);
                return NotFound(ApiResponse<TableDetailResponse>.Failure($"Table with ID {tableId} not found"));
            }

            // Get columns for this table
            var columns = await _schemaService.GetStoredColumnsAsync(tableId);

            // Build the detailed response
            var response = new TableDetailResponse
            {
                TableId = table.TableId,
                TableName = table.TableName,
                SchemaName = table.SchemaName,
                RowCount = null, // Can be populated from actual DB if needed
                Columns = columns.Select(c => new ColumnDetailInfo
                {
                    ColumnId = c.ColumnId,
                    Name = c.ColumnName,
                    DataType = c.DataType,
                    IsNullable = c.IsNullable,
                    DefaultValue = c.DefaultValue,
                    Constraints = BuildConstraints(c)
                }).ToList(),
                PrimaryKeys = columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName).ToList(),
                ForeignKeys = null, // Can be populated from FK metadata if needed
                Indexes = null // Can be populated from index metadata if needed
            };

            return Ok(ApiResponse<TableDetailResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting table details for table {TableId} in project {ProjectId}", tableId, projectId);
            return StatusCode(500, ApiResponse<TableDetailResponse>.Failure("An error occurred while retrieving table details"));
        }
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

    /// <summary>
    /// Get detailed column information by column ID
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="tableId">The table ID</param>
    /// <param name="columnId">The column ID</param>
    /// <returns>Detailed column information</returns>
    [HttpGet("projects/{projectId}/tables/{tableId}/columns/{columnId}")]
    [ProducesResponseType(typeof(ApiResponse<ColumnDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ColumnDetailResponse>> GetColumnDetail(int projectId, int tableId, int columnId)
    {
        try
        {
            _logger.LogInformation("Getting column details for column {ColumnId} in table {TableId} in project {ProjectId}", columnId, tableId, projectId);

            // Get column metadata
            var column = await _schemaService.GetColumnByIdAsync(columnId);
            if (column == null)
            {
                return NotFound(ApiResponse<ColumnDetailResponse>.Failure($"Column with ID {columnId} not found"));
            }

            // Get table metadata to get table name
            var table = await _schemaService.GetTableByIdAsync(tableId);
            if (table == null)
            {
                return NotFound(ApiResponse<ColumnDetailResponse>.Failure($"Table with ID {tableId} not found"));
            }

            // Build the detailed response
            var response = new ColumnDetailResponse
            {
                ColumnId = column.ColumnId,
                ColumnName = column.ColumnName,
                TableName = table.TableName,
                TableId = column.TableId,
                SchemaName = table.SchemaName,
                DataType = column.DataType,
                MaxLength = column.MaxLength,
                Precision = column.Precision,
                Scale = column.Scale,
                IsNullable = column.IsNullable,
                IsPrimaryKey = column.IsPrimaryKey,
                IsForeignKey = column.IsForeignKey,
                IsIdentity = false, // Can be enhanced from metadata
                DefaultValue = column.DefaultValue,
                Constraints = BuildConstraints(column),
                Description = column.Description,
                ForeignKeyReference = null // Can be populated from FK metadata if needed
            };

            return Ok(ApiResponse<ColumnDetailResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting column details for column {ColumnId} in table {TableId} in project {ProjectId}", columnId, tableId, projectId);
            return StatusCode(500, ApiResponse<ColumnDetailResponse>.Failure("An error occurred while retrieving column details"));
        }
    }

    /// <summary>
    /// Build constraints list for a column
    /// </summary>
    private static List<string> BuildConstraints(ColumnMetadataDto column)
    {
        var constraints = new List<string>();

        if (column.IsPrimaryKey)
            constraints.Add("PRIMARY KEY");

        if (column.IsForeignKey)
            constraints.Add("FOREIGN KEY");

        if (!column.IsNullable)
            constraints.Add("NOT NULL");

        if (!string.IsNullOrEmpty(column.DefaultValue))
            constraints.Add($"DEFAULT {column.DefaultValue}");

        return constraints;
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
