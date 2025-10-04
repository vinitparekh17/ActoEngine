using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.Schema;

public interface ISchemaService
{
    Task<List<string>> GetAllTablesAsync(string connectionString);
    Task<TableSchemaResponse> GetTableSchemaAsync(string connectionString, string tableName);
    Task<List<DatabaseTableInfo>> GetDatabaseStructureAsync(string connectionString);
}

public class DatabaseTableInfo
{
    public required string SchemaName { get; set; }
    public required string TableName { get; set; }
}

public class SchemaService(
    ISchemaSyncRepository schemaRepository,
    ILogger<SchemaService> logger) : ISchemaService
{
    private readonly ISchemaSyncRepository _schemaRepository = schemaRepository;
    private readonly ILogger<SchemaService> _logger = logger;

    public async Task<List<string>> GetAllTablesAsync(string connectionString)
    {
        try
        {
            _logger.LogInformation("Retrieving all tables from database");
            return await _schemaRepository.GetAllTables(connectionString);
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
            _logger.LogInformation("Retrieving schema for table: {TableName}", tableName);
            
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }

            return await _schemaRepository.ReadTableSchema(connectionString, tableName);
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
            _logger.LogInformation("Retrieving database structure");
            
            var tables = await _schemaRepository.GetAllTables(connectionString);
            
            // Parse the schema.table format returned by GetAllTables query
            var databaseStructure = new List<DatabaseTableInfo>();
            
            foreach (var table in tables)
            {
                // Assuming the GetAllTables query returns "SchemaName.TableName" format
                // If it returns just table names, we'll default to "dbo" schema
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
            
            return databaseStructure.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database structure");
            throw;
        }
    }
}
