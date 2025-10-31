using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Services.Schema;

public interface ISchemaService
{
    Task<List<string>> GetAllTablesAsync(string connectionString);
    Task<TableSchemaResponse> GetTableSchemaAsync(string connectionString, string tableName);
    Task<List<DatabaseTableInfo>> GetDatabaseStructureAsync(string connectionString);
    Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(string connectionString);
    Task<List<Dictionary<string, object?>>> GetTableDataAsync(string connectionString, string tableName, int limit = 100);
    Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(string connectionString, IEnumerable<string> tableNames);
    // Methods for stored metadata
    Task<List<TableMetadataDto>> GetStoredTablesAsync(int projectId);
    Task<List<ColumnMetadataDto>> GetStoredColumnsAsync(int tableId);
    Task<List<StoredProcedureMetadataDto>> GetStoredProceduresMetadataAsync(int projectId);
    Task<TableSchemaResponse> GetStoredTableSchemaAsync(int projectId, string tableName);

    // Tree structure
    Task<TreeNode> GetDatabaseTreeAsync(int projectId, string databaseName);
}

public class SchemaService(
    ISchemaRepository schemaRepository,
    ILogger<SchemaService> logger) : ISchemaService
{
    private readonly ISchemaRepository _schemaRepository = schemaRepository;
    private readonly ILogger<SchemaService> _logger = logger;

    public async Task<List<string>> GetAllTablesAsync(string connectionString)
    {
        try
        {
            return await _schemaRepository.GetAllTablesAsync(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tables from database");
            throw;
        }
    }

    public async Task<TableSchemaResponse> GetTableSchemaAsync(string connectionString, string tableName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }

            return await _schemaRepository.ReadTableSchemaAsync(connectionString, tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schema for table: {TableName}", tableName);
            throw;
        }
    }

    public async Task<List<DatabaseTableInfo>> GetDatabaseStructureAsync(string connectionString)
    {
        try
        {
            var tables = await _schemaRepository.GetAllTablesAsync(connectionString);

            var databaseStructure = new List<DatabaseTableInfo>();

            foreach (var table in tables)
            {
                var parts = table.Split('.');
                if (parts.Length == 2)
                {
                    databaseStructure.Add(new DatabaseTableInfo
                    {
                        SchemaName = parts[0],
                        TableName = parts[1]
                    });
                }
                else
                {
                    databaseStructure.Add(new DatabaseTableInfo
                    {
                        SchemaName = "dbo",
                        TableName = table
                    });
                }
            }

            return [.. databaseStructure.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database structure");
            throw;
        }
    }

    public async Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(string connectionString)
    {
        try
        {
            return await _schemaRepository.GetStoredProceduresAsync(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored procedures from database");
            throw;
        }
    }

    public async Task<List<Dictionary<string, object?>>> GetTableDataAsync(string connectionString, string tableName, int limit = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }

            // Validate table name format: allows [schema.]table (alphanumeric and underscores only)
            var tableNameRegex = new Regex(@"^[a-zA-Z0-9_]+(\.[a-zA-Z0-9_]+)?$", RegexOptions.Compiled);
            if (!tableNameRegex.IsMatch(tableName))
            {
                throw new ArgumentException($"Invalid table name format: '{tableName}'", nameof(tableName));
            }

            // Extract just the table name (without schema) for comparison
            var parts = tableName.Split('.');
            string tableNameOnly = parts.Length == 2 ? parts[1] : tableName;

            // Verify table exists in the database
            var availableTables = await GetAllTablesAsync(connectionString);
            
            // Normalize comparison: extract unqualified names from availableTables and compare case-insensitively
            // This handles both schema-qualified (e.g., "dbo.Users") and unqualified (e.g., "Users") table names
            var unqualifiedTableNames = availableTables
                .Select(t => t.Split('.').Last())
                .ToList();
            
            // Check if either the original tableName or its unqualified form exists
            bool tableExists = availableTables.Contains(tableName, StringComparer.OrdinalIgnoreCase) ||
                               unqualifiedTableNames.Contains(tableNameOnly, StringComparer.OrdinalIgnoreCase);
            
            if (!tableExists)
            {
                throw new ArgumentException($"Table '{tableName}' not found in database", nameof(tableName));
            }

            return await _schemaRepository.GetTableDataAsync(connectionString, tableName, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data from table: {TableName}", tableName);
            throw;
        }
    }

    public async Task<List<TableMetadataDto>> GetStoredTablesAsync(int projectId)
    {
        try
        {
            return await _schemaRepository.GetStoredTablesAsync(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored tables for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<List<ColumnMetadataDto>> GetStoredColumnsAsync(int tableId)
    {
        try
        {
            return await _schemaRepository.GetStoredColumnsAsync(tableId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored columns for table {TableId}", tableId);
            throw;
        }
    }

    public async Task<List<StoredProcedureMetadataDto>> GetStoredProceduresMetadataAsync(int projectId)
    {
        try
        {
            return await _schemaRepository.GetStoredStoredProceduresAsync(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored procedure metadata for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<TableSchemaResponse> GetStoredTableSchemaAsync(int projectId, string tableName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }

            return await _schemaRepository.GetStoredTableSchemaAsync(projectId, tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored table schema for table {TableName} in project {ProjectId}", tableName, projectId);
            throw;
        }
    }

    public async Task<TreeNode> GetDatabaseTreeAsync(int projectId, string databaseName)
    {
        try
        {
            var tables = await GetStoredTablesAsync(projectId);
            var procedures = await GetStoredProceduresMetadataAsync(projectId);

            var tablesChildren = new List<TreeNode>();
            foreach (var table in tables.OrderBy(t => t.TableName))
            {
                // Get columns for this table
                var columns = await GetStoredColumnsAsync(table.TableId);

                var columnNodes = columns
                    .OrderBy(c => c.ColumnOrder)
                    .Select(col => new TreeNode
                    {
                        Id = $"col-{table.TableId}-{col.ColumnId}",
                        Name = col.ColumnName,
                        Type = "column"
                    })
                    .ToList();

                tablesChildren.Add(new TreeNode
                {
                    Id = $"table-{table.TableId}",
                    Name = table.TableName,
                    Type = "table",
                    Children = columnNodes
                });
            }

            var tablesFolder = new TreeNode
            {
                Id = $"db-{projectId}-tables",
                Name = "Tables",
                Type = "tables-folder",
                Children = tablesChildren
            };

            // Build Stored Procedures folder
            var spChildren = procedures
                .OrderBy(sp => sp.ProcedureName)
                .Select(sp => new TreeNode
                {
                    Id = $"sp-{sp.SpId}",
                    Name = sp.ProcedureName,
                    Type = "stored-procedure"
                })
                .ToList();

            var storedProceduresFolder = new TreeNode
            {
                Id = $"db-{projectId}-sps",
                Name = "Stored Procedures",
                Type = "stored-procedures-folder",
                Children = spChildren
            };

            // Build Functions folder (placeholder for future)
            var functionsFolder = new TreeNode
            {
                Id = $"db-{projectId}-funcs",
                Name = "Functions",
                Type = "functions-folder",
                Children = []
            };

            // Build Programmability folder
            var programmabilityFolder = new TreeNode
            {
                Id = $"db-{projectId}-prog",
                Name = "Programmability",
                Type = "programmability-folder",
                Children =
                [
                    storedProceduresFolder,
                    functionsFolder
                ]
            };

            // Build root database node
            var databaseNode = new TreeNode
            {
                Id = $"db-{projectId}",
                Name = databaseName,
                Type = "database",
                Children =
                [
                    tablesFolder,
                    programmabilityFolder
                ]
            };

            return databaseNode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building database tree for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(
        string connectionString, 
        IEnumerable<string> tableNames)
    {
        try
        {
            return await _schemaRepository.GetForeignKeysAsync(connectionString, tableNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving foreign keys from database");
            throw;
        }
    }
}