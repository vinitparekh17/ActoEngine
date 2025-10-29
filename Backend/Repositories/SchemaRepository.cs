using System.Data;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.Sql.Queries;
using Dapper;

namespace ActoEngine.WebApi.Repositories;

public interface ISchemaSyncRepository
{
    Task<int> SyncTablesAsync(int projectId, IEnumerable<string> tableNames, IDbConnection connection, IDbTransaction transaction);
    Task<int> SyncColumnsAsync(int tableId, IEnumerable<ColumnMetadata> columns, IDbConnection connection, IDbTransaction transaction);
    Task<int> SyncStoredProceduresAsync(int projectId, int clientId, IEnumerable<StoredProcedureMetadata> procedures, int userId, IDbConnection connection, IDbTransaction transaction);
    Task<IEnumerable<(int TableId, string TableName)>> GetProjectTablesAsync(int projectId, IDbConnection connection, IDbTransaction transaction);
    Task<TableSchemaResponse> ReadTableSchemaAsync(string connectionString, string tableName);
    Task<List<string>> GetAllTablesAsync(string connectionString);
    Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(string connectionString);

    // Methods to retrieve stored metadata
    Task<List<TableMetadataDto>> GetStoredTablesAsync(int projectId);
    Task<List<ColumnMetadataDto>> GetStoredColumnsAsync(int tableId);
    Task<List<StoredProcedureMetadataDto>> GetStoredStoredProceduresAsync(int projectId);
    Task<TableSchemaResponse> GetStoredTableSchemaAsync(int projectId, string tableName);
    Task<List<Dictionary<string, object?>>> GetTableDataAsync(string connectionString, string tableName, int limit);

    Task<int> SyncForeignKeysAsync(
        int projectId,
        IEnumerable<ForeignKeyScanResult> foreignKeys,
        IDbConnection connection,
        IDbTransaction transaction);

    Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(string connectionString, IEnumerable<string> tableNames);
}

public class SchemaSyncRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<SchemaSyncRepository> logger)
    : BaseRepository(connectionFactory, logger), ISchemaSyncRepository
{
    public async Task<int> SyncTablesAsync(
        int projectId,
        IEnumerable<string> tableNames,
        IDbConnection connection,
        IDbTransaction transaction)
    {
        try
        {
            var count = 0;
            foreach (var tableName in tableNames)
            {
                await connection.ExecuteAsync(
                    SchemaSyncQueries.InsertTableMetadata,
                    new { ProjectId = projectId, TableName = tableName },
                    transaction);
                count++;
            }
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing tables for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<int> SyncColumnsAsync(
        int tableId,
        IEnumerable<ColumnMetadata> columns,
        IDbConnection connection,
        IDbTransaction transaction)
    {
        try
        {
            var count = 0;
            foreach (var column in columns)
            {
                await connection.ExecuteAsync(
                    SchemaSyncQueries.InsertColumnMetadata,
                    new
                    {
                        TableId = tableId,
                        column.ColumnName,
                        column.DataType,
                        column.MaxLength,
                        column.Precision,
                        column.Scale,
                        column.IsNullable,
                        column.IsPrimaryKey,
                        column.IsForeignKey,
                        column.ColumnOrder
                    },
                    transaction);
                count++;
            }
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing columns for table {TableId}", tableId);
            throw;
        }
    }

    public async Task<int> SyncStoredProceduresAsync(
        int projectId,
        int clientId,
        IEnumerable<StoredProcedureMetadata> procedures,
        int userId,
        IDbConnection connection,
        IDbTransaction transaction)
    {
        try
        {
            var count = 0;
            foreach (var procedure in procedures)
            {
                await connection.ExecuteAsync(
                    SchemaSyncQueries.InsertSpMetadata,
                    new
                    {
                        ProjectId = projectId,
                        ClientId = clientId,
                        ProcedureName = procedure.ProcedureName,
                        Definition = procedure.Definition,
                        UserId = userId
                    },
                    transaction);
                count++;
            }
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing stored procedures for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<IEnumerable<(int TableId, string TableName)>> GetProjectTablesAsync(
        int projectId,
        IDbConnection connection,
        IDbTransaction transaction)
    {
        try
        {
            return await connection.QueryAsync<(int, string)>(
                SchemaSyncQueries.GetTableMetaByProjectId,
                new { ProjectId = projectId },
                transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tables for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<TableSchemaResponse> ReadTableSchemaAsync(string connectionString, string tableName)
    {
        // Note: Uses external connection string, cannot use BaseRepository methods
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithConnectionString(connectionString);

            var columns = await connection.QueryAsync<ColumnSchema>(
                SchemaSyncQueries.GetTableSchema,
                new { TableName = tableName });

            var columnsList = columns.ToList();

            return new TableSchemaResponse
            {
                TableName = tableName,
                SchemaName = columnsList.FirstOrDefault()?.SchemaName ?? "dbo",
                Columns = columnsList,
                PrimaryKeys = [.. columnsList.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName)]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading table schema for table {TableName}", tableName);
            throw;
        }
    }

    public async Task<List<string>> GetAllTablesAsync(string connectionString)
    {
        // Note: Uses external connection string, cannot use BaseRepository methods
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithConnectionString(connectionString);

            var tables = await connection.QueryAsync<string>(SchemaSyncQueries.GetAllTables);
            return [.. tables];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tables from connection string");
            throw;
        }
    }

    public async Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(string connectionString)
    {
        // Note: Uses external connection string, cannot use BaseRepository methods
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithConnectionString(connectionString);

            var procedures = await connection.QueryAsync<StoredProcedureMetadata>(
                SchemaSyncQueries.GetTargetStoredProcedures);

            return [.. procedures];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored procedures from connection string");
            throw;
        }
    }

    public async Task<List<TableMetadataDto>> GetStoredTablesAsync(int projectId)
    {
        var tables = await QueryAsync<TableMetadataDto>(
            SchemaSyncQueries.GetStoredTables,
            new { ProjectId = projectId });

        return [.. tables];
    }

    public async Task<List<ColumnMetadataDto>> GetStoredColumnsAsync(int tableId)
    {
        var columns = await QueryAsync<ColumnMetadataDto>(
            SchemaSyncQueries.GetStoredColumns,
            new { TableId = tableId });

        return [.. columns];
    }

    public async Task<List<StoredProcedureMetadataDto>> GetStoredStoredProceduresAsync(int projectId)
    {
        var procedures = await QueryAsync<StoredProcedureMetadataDto>(
            SchemaSyncQueries.GetStoredStoredProcedures,
            new { ProjectId = projectId });

        return [.. procedures];
    }

    public async Task<TableSchemaResponse> GetStoredTableSchemaAsync(int projectId, string tableName)
    {
        // First get the table
        var table = await QueryFirstOrDefaultAsync<(int TableId, string TableName)>(
            SchemaSyncQueries.GetStoredTableByName,
            new { ProjectId = projectId, TableName = tableName });

        if (table.TableId == 0)
        {
            throw new InvalidOperationException($"Table '{tableName}' not found for project {projectId}");
        }

        // Then get the columns with foreign key information
        var columns = await QueryAsync<dynamic>(
            SchemaSyncQueries.GetStoredTableColumns,
            new { TableId = table.TableId });

        var columnsList = columns.Select(c => new ColumnSchema
        {
            SchemaName = "dbo",
            ColumnName = c.ColumnName,
            DataType = c.DataType,
            MaxLength = c.MaxLength,
            Precision = c.Precision,
            Scale = c.Scale,
            IsNullable = c.IsNullable,
            IsPrimaryKey = c.IsPrimaryKey,
            IsIdentity = false, // Set from metadata if needed
            IsForeignKey = c.IsForeignKey,
            DefaultValue = c.DefaultValue ?? string.Empty,
            ForeignKeyInfo = c.IsForeignKey && c.ReferencedTable != null
                ? new ForeignKeyInfo
                {
                    ReferencedTable = c.ReferencedTable,
                    ReferencedColumn = c.ReferencedColumn ?? string.Empty,
                    DisplayColumn = null, // User will set this in the form builder
                    OnDeleteAction = c.OnDeleteAction ?? "NO ACTION",
                    OnUpdateAction = c.OnUpdateAction ?? "NO ACTION"
                }
                : null
        }).ToList();

        return new TableSchemaResponse
        {
            TableName = tableName,
            SchemaName = "dbo", // Default schema
            Columns = columnsList,
            PrimaryKeys = [.. columnsList.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName)]
        };
    }

    public async Task<List<Dictionary<string, object?>>> GetTableDataAsync(string connectionString, string tableName, int limit)
    {
        // Note: Uses external connection string, cannot use BaseRepository methods
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithConnectionString(connectionString);

            // Parse schema and table name
            var parts = tableName.Split('.');
            string schemaName;
            string tableNameOnly;

            if (parts.Length == 2)
            {
                schemaName = parts[0];
                tableNameOnly = parts[1];
            }
            else
            {
                tableNameOnly = tableName;
                // Auto-detect schema if not provided
                schemaName = await connection.QueryFirstOrDefaultAsync<string>(
                    SchemaSyncQueries.FindTableSchema,
                    new { TableName = tableNameOnly }) ?? "dbo";
                _logger.LogInformation("Auto-detected schema '{Schema}' for table '{TableName}'", schemaName, tableNameOnly);
            }

            // Verify table exists in sys.tables
            var exists = await connection.QueryFirstOrDefaultAsync<int>(
                SchemaSyncQueries.VerifyTableExists,
                new { SchemaName = schemaName, TableName = tableNameOnly });

            if (exists == 0)
            {
                // Get available tables for better error message
                var availableTables = await connection.QueryAsync<string>(
                    "SELECT s.name + '.' + t.name FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id");
                var tableList = string.Join(", ", availableTables.Take(10));
                _logger.LogWarning("Table '{TableName}' not found. Schema: '{Schema}', TableOnly: '{TableOnly}'. Available tables: {Tables}",
                    tableName, schemaName, tableNameOnly, tableList);
                throw new InvalidOperationException($"Table '{tableName}' not found in database");
            }

            // Get safely quoted table name
            var quotedTableName = await connection.QueryFirstOrDefaultAsync<string>(
                SchemaSyncQueries.GetQuotedTableName,
                new { SchemaName = schemaName, TableName = tableNameOnly });

            // Execute query with proper parameterization
            var sql = $"SELECT TOP (@Limit) * FROM {quotedTableName}";
            using var reader = await connection.ExecuteReaderAsync(sql, new { Limit = limit });

            var results = new List<Dictionary<string, object?>>();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var fieldName = reader.GetName(i);
                    object? fieldValue = null;

                    try
                    {
                        if (!reader.IsDBNull(i))
                        {
                            var fieldType = reader.GetFieldType(i);

                            // Handle problematic types that don't convert well
                            if (fieldType.FullName == "Microsoft.SqlServer.Types.SqlGeography" ||
                                fieldType.FullName == "Microsoft.SqlServer.Types.SqlGeometry" ||
                                fieldType.FullName == "Microsoft.SqlServer.Types.SqlHierarchyId")
                            {
                                // Convert spatial/hierarchyid types to string representation
                                fieldValue = reader.GetValue(i)?.ToString();
                            }
                            else if (fieldType == typeof(byte[]))
                            {
                                // Convert binary data to base64 string for JSON serialization
                                var bytes = (byte[])reader.GetValue(i);
                                fieldValue = Convert.ToBase64String(bytes);
                            }
                            else
                            {
                                fieldValue = reader.GetValue(i);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue processing other fields
                        _logger.LogWarning(ex, "Error reading field '{FieldName}' at index {Index}, using null value", fieldName, i);
                        fieldValue = $"<Error reading value: {ex.Message}>";
                    }

                    row[fieldName] = fieldValue;
                }
                results.Add(row);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting table data for table {TableName}", tableName);
            throw;
        }
    }
public async Task<int> SyncForeignKeysAsync(
    int projectId,
    IEnumerable<ForeignKeyScanResult> foreignKeys,
    IDbConnection connection,
    IDbTransaction transaction)
{
    try
    {
        var count = 0;
        foreach (var fk in foreignKeys)
        {
            // Insert by resolving IDs from names within SQL to avoid FK issues
            await connection.ExecuteAsync(
                SchemaSyncQueries.InsertForeignKeyMetadataByNames,
                new
                {
                    ProjectId = projectId,
                    fk.TableName,
                    fk.ColumnName,
                    fk.ReferencedTable,
                    fk.ReferencedColumn,
                    fk.OnDeleteAction,
                    fk.OnUpdateAction
                },
                transaction);
            count++;
        }
        return count;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error syncing foreign keys for project {ProjectId}", projectId);
        throw;
    }
}

    public async Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(
        string connectionString, 
        IEnumerable<string> tableNames)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithConnectionString(connectionString);

            var foreignKeys = await connection.QueryAsync<dynamic>(
                SchemaSyncQueries.GetForeignKeysForTables,
                new { TableNames = tableNames });

            // Map dynamic results to ForeignKeyScanResult with names and actions
            var result = foreignKeys.Select(fk => new ForeignKeyScanResult
            {
                TableName = (string)fk.TableName,
                ColumnName = (string)fk.ColumnName,
                ReferencedTable = (string)fk.ReferencedTable,
                ReferencedColumn = (string)fk.ReferencedColumn,
                OnDeleteAction = (string)(fk.OnDeleteAction ?? "NO ACTION"),
                OnUpdateAction = (string)(fk.OnUpdateAction ?? "NO ACTION")
            });

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting foreign keys from connection string");
            throw;
        }
    }
}