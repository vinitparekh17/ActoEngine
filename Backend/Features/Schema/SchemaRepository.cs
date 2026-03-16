using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using Dapper;
using System.Data;

namespace ActoEngine.WebApi.Features.Schema;

public interface ISchemaRepository
{
    Task<int> SyncTablesAsync(int projectId, IEnumerable<(string TableName, string SchemaName)> tableNames, IDbConnection connection, IDbTransaction transaction);
    Task<int> SyncColumnsAsync(int tableId, IEnumerable<ColumnMetadata> columns, IDbConnection connection, IDbTransaction transaction);
    Task<int> SyncIndexesAsync(int projectId, int tableId, IEnumerable<IndexScanResult> indexes, IDbConnection connection, IDbTransaction transaction);
    Task<int> SyncStoredProceduresAsync(int projectId, int clientId, IEnumerable<StoredProcedureMetadata> procedures, int userId, IDbConnection connection, IDbTransaction transaction);
    Task<IEnumerable<(int TableId, string TableName, string SchemaName)>> GetProjectTablesAsync(int projectId, IDbConnection connection, IDbTransaction transaction);
    Task<TableSchemaResponse> ReadTableSchemaAsync(string connectionString, string tableName);
    Task<List<string>> GetAllTablesAsync(string connectionString);
    Task<List<(string TableName, string SchemaName)>> GetAllTablesWithSchemaAsync(string connectionString);
    Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(string connectionString);
    Task<IEnumerable<dynamic>> GetStoredProcedureModifyDatesAsync(string connectionString);
    Task<List<SpHashInfo>> GetSpHashesAsync(int projectId);
    Task<bool> UpdateSpDefinitionAndHashAsync(
        int projectId,
        int spId,
        string definition,
        string definitionHash,
        DateTime sourceModifyDate,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null);
    Task<bool> SoftDeleteTableAsync(int projectId, int tableId);
    Task<bool> SoftDeleteSpAsync(int projectId, int spId);

    // Methods to retrieve stored metadata
    // Lightweight list methods (minimal bandwidth)
    Task<List<TableListDto>> GetTablesListAsync(int projectId);
    Task<List<StoredProcedureListDto>> GetStoredProceduresListAsync(int projectId);

    // Full metadata methods (for detail views)
    Task<List<TableMetadataDto>> GetStoredTablesAsync(int projectId);
    Task<List<ColumnMetadataDto>> GetStoredColumnsAsync(int tableId);
    Task<List<StoredIndexDto>> GetStoredIndexesAsync(int tableId);
    Task<List<StoredForeignKeyDto>> GetStoredForeignKeysAsync(int tableId);
    Task<List<StoredProcedureMetadataDto>> GetStoredStoredProceduresAsync(int projectId);
    Task<TableSchemaResponse> GetStoredTableSchemaAsync(int projectId, string tableName, string schemaName);

    // Methods to retrieve individual entities by ID
    Task<TableMetadataDto?> GetTableByIdAsync(int tableId);
    Task<ColumnMetadataDto?> GetColumnByIdAsync(int columnId);
    Task<StoredProcedureMetadataDto?> GetSpByIdAsync(int spId);

    Task<int> SyncForeignKeysAsync(
        int projectId,
        IEnumerable<ForeignKeyScanResult> foreignKeys,
        IDbConnection connection,
        IDbTransaction transaction);

    Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(string connectionString, IEnumerable<string> tableNames);
    Task<IEnumerable<IndexScanResult>> GetIndexesAsync(string connectionString, IEnumerable<string> tableNames);
}

public class SchemaRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<SchemaRepository> logger)
    : BaseRepository(connectionFactory, logger), ISchemaRepository
{
    public async Task<int> SyncTablesAsync(
        int projectId,
        IEnumerable<(string TableName, string SchemaName)> tableSchemas,
        IDbConnection connection,
        IDbTransaction transaction)
    {
        try
        {
            var count = 0;
            foreach (var (tableName, schemaName) in tableSchemas)
            {
                var restored = await connection.ExecuteAsync(
                    SchemaSyncQueries.RestoreOrUpsertTable,
                    new { ProjectId = projectId, TableName = tableName, SchemaName = schemaName },
                    transaction);

                if (restored == 0)
                {
                    await connection.ExecuteAsync(
                        SchemaSyncQueries.InsertTableMetadata,
                        new { ProjectId = projectId, TableName = tableName, SchemaName = schemaName },
                        transaction);
                }

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
                        column.IsIdentity,
                        column.DefaultValue,
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

    /// <summary>
    /// Syncs stored procedure metadata for a project and client into the metadata store.
    /// </summary>
    /// <param name="projectId">Identifier of the project owning the stored procedures.</param>
    /// <param name="clientId">Identifier of the client associated with the stored procedures.</param>
    /// <param name="procedures">Collection of stored procedure metadata to be persisted.</param>
    /// <param name="userId">Identifier of the user performing the synchronization.</param>
    /// <returns>The number of stored procedures that were processed.</returns>
    /// <exception cref="Exception">Propagates any exception encountered while executing database commands.</exception>
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
                        procedure.SchemaName,
                        procedure.ProcedureName,
                        procedure.Definition,
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

    public async Task<IEnumerable<(int TableId, string TableName, string SchemaName)>> GetProjectTablesAsync(
        int projectId,
        IDbConnection connection,
        IDbTransaction transaction)
    {
        try
        {
            return await connection.QueryAsync<(int, string, string)>(
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

    public async Task<List<(string TableName, string SchemaName)>> GetAllTablesWithSchemaAsync(string connectionString)
    {
        // Note: Uses external connection string, cannot use BaseRepository methods
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithConnectionString(connectionString);

            var tablesWithSchema = await connection.QueryAsync<(string TableName, string SchemaName)>(SchemaSyncQueries.GetTargetTablesWithSchema);
            return [.. tablesWithSchema];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tables with schema from connection string");
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

    public async Task<IEnumerable<dynamic>> GetStoredProcedureModifyDatesAsync(string connectionString)
    {
        // Note: Uses external connection string, cannot use BaseRepository methods
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithConnectionString(connectionString);
            return await connection.QueryAsync<dynamic>(SchemaSyncQueries.GetTargetSpModifyDates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored procedure modify dates from connection string");
            throw;
        }
    }

    public async Task<int> SyncIndexesAsync(
        int projectId,
        int tableId,
        IEnumerable<IndexScanResult> indexes,
        IDbConnection connection,
        IDbTransaction transaction)
    {
        try
        {
            await connection.ExecuteAsync(
                SchemaSyncQueries.DeleteIndexesByTable,
                new { TableId = tableId },
                transaction);

            var columnLookup = (await connection.QueryAsync<(int ColumnId, string ColumnName)>(
                "SELECT ColumnId, ColumnName FROM ColumnsMetadata WHERE TableId = @TableId",
                new { TableId = tableId },
                transaction))
                .ToDictionary(
                    row => row.ColumnName,
                    row => row.ColumnId,
                    StringComparer.OrdinalIgnoreCase);

            var count = 0;
            foreach (var index in indexes
                .GroupBy(i => i.IndexName, StringComparer.OrdinalIgnoreCase))
            {
                var head = index.First();
                var indexId = await connection.ExecuteScalarAsync<int>(
                    SchemaSyncQueries.InsertIndexMetadata,
                    new
                    {
                        TableId = tableId,
                        head.IndexName,
                        head.IsUnique,
                        head.IsPrimaryKey
                    },
                    transaction);

                foreach (var column in index
                    .Where(c => !c.IsIncludedColumn)
                    .OrderBy(c => c.ColumnOrder))
                {
                    if (!columnLookup.TryGetValue(column.ColumnName, out var columnId))
                    {
                        continue;
                    }

                    await connection.ExecuteAsync(
                        SchemaSyncQueries.InsertIndexColumnMetadata,
                        new
                        {
                            IndexId = indexId,
                            ColumnId = columnId,
                            ColumnOrder = column.ColumnOrder
                        },
                        transaction);
                }

                count++;
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing indexes for table {TableId} in project {ProjectId}", tableId, projectId);
            throw;
        }
    }

    public async Task<List<SpHashInfo>> GetSpHashesAsync(int projectId)
    {
        var hashes = await QueryAsync<SpHashInfo>(
            SchemaSyncQueries.GetSpHashes,
            new { ProjectId = projectId });

        return [.. hashes];
    }

    public async Task<bool> UpdateSpDefinitionAndHashAsync(
        int projectId,
        int spId,
        string definition,
        string definitionHash,
        DateTime sourceModifyDate,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null)
    {
        var parameters = new
        {
            ProjectId = projectId,
            SpId = spId,
            Definition = definition,
            DefinitionHash = definitionHash,
            SourceModifyDate = sourceModifyDate
        };

        int affected;
        if (connection != null)
        {
            affected = await connection.ExecuteAsync(
                SchemaSyncQueries.UpdateSpDefinitionAndHash,
                parameters,
                transaction);
        }
        else
        {
            affected = await ExecuteAsync(
                SchemaSyncQueries.UpdateSpDefinitionAndHash,
                parameters);
        }

        return affected > 0;
    }

    public async Task<bool> SoftDeleteTableAsync(int projectId, int tableId)
    {
        var affected = await ExecuteAsync(
            SchemaSyncQueries.SoftDeleteTable,
            new { ProjectId = projectId, TableId = tableId });

        return affected > 0;
    }

    public async Task<bool> SoftDeleteSpAsync(int projectId, int spId)
    {
        var affected = await ExecuteAsync(
            SchemaSyncQueries.SoftDeleteSp,
            new { ProjectId = projectId, SpId = spId });

        return affected > 0;
    }

    // Lightweight list methods
    public async Task<List<TableListDto>> GetTablesListAsync(int projectId)
    {
        var tables = await QueryAsync<TableListDto>(
            SchemaSyncQueries.GetTablesListMinimal,
            new { ProjectId = projectId });

        return [.. tables];
    }

    public async Task<List<StoredProcedureListDto>> GetStoredProceduresListAsync(int projectId)
    {
        var procedures = await QueryAsync<StoredProcedureListDto>(
            SchemaSyncQueries.GetStoredProceduresListMinimal,
            new { ProjectId = projectId });

        return [.. procedures];
    }

    // Full metadata methods
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

    public async Task<List<StoredIndexDto>> GetStoredIndexesAsync(int tableId)
    {
        var rows = await QueryAsync<StoredIndexQueryRow>(
            SchemaSyncQueries.GetStoredIndexes,
            new { TableId = tableId });

        return rows
            .GroupBy(r => new { r.IndexId, r.TableId, r.IndexName, r.IsUnique, r.IsPrimaryKey })
            .Select(g => new StoredIndexDto
            {
                IndexId = g.Key.IndexId,
                TableId = g.Key.TableId,
                IndexName = g.Key.IndexName,
                IsUnique = g.Key.IsUnique,
                IsPrimaryKey = g.Key.IsPrimaryKey,
                Columns = g.Where(r => r.ColumnId.HasValue && r.ColumnName != null)
                    .Select(r => new StoredIndexColumnDto
                    {
                        ColumnId = r.ColumnId!.Value,
                        ColumnName = r.ColumnName!,
                        ColumnOrder = r.ColumnOrder ?? 0,
                        IsIncludedColumn = r.IsIncludedColumn
                    })
                    .OrderBy(c => c.ColumnOrder)
                    .ToList()
            })
            .ToList();
    }

    public async Task<List<StoredForeignKeyDto>> GetStoredForeignKeysAsync(int tableId)
    {
        var foreignKeys = await QueryAsync<StoredForeignKeyDto>(
            SchemaSyncQueries.GetStoredForeignKeys,
            new { TableId = tableId });

        return [.. foreignKeys];
    }

    public async Task<List<StoredProcedureMetadataDto>> GetStoredStoredProceduresAsync(int projectId)
    {
        var procedures = await QueryAsync<StoredProcedureMetadataDto>(
            SchemaSyncQueries.GetStoredStoredProcedures,
            new { ProjectId = projectId });

        return [.. procedures];
    }

    public async Task<TableMetadataDto?> GetTableByIdAsync(int tableId)
    {
        return await QueryFirstOrDefaultAsync<TableMetadataDto>(
            SchemaSyncQueries.GetTableById,
            new { TableId = tableId });
    }

    public async Task<ColumnMetadataDto?> GetColumnByIdAsync(int columnId)
    {
        return await QueryFirstOrDefaultAsync<ColumnMetadataDto>(
            SchemaSyncQueries.GetColumnById,
            new { ColumnId = columnId });
    }

    public async Task<StoredProcedureMetadataDto?> GetSpByIdAsync(int spId)
    {
        return await QueryFirstOrDefaultAsync<StoredProcedureMetadataDto>(
            SchemaSyncQueries.GetSpById,
            new { SpId = spId });
    }

    public async Task<TableSchemaResponse> GetStoredTableSchemaAsync(int projectId, string tableName, string schemaName)
    {
        // First get the table
        var table = await QueryFirstOrDefaultAsync<(int TableId, string TableName, string SchemaName)>(
            SchemaSyncQueries.GetStoredTableByName,
            new { ProjectId = projectId, TableName = tableName, SchemaName = schemaName });

        if (table.TableId == 0)
        {
            throw new InvalidOperationException($"Table '{schemaName}.{tableName}' not found for project {projectId}");
        }

        // Then get the columns with foreign key information
        var columns = await QueryAsync<dynamic>(
            SchemaSyncQueries.GetStoredTableColumns,
            new { table.TableId });

        var columnsList = columns.Select(c => new ColumnSchema
        {
            SchemaName = schemaName,
            ColumnName = c.ColumnName,
            DataType = c.DataType,
            MaxLength = c.MaxLength,
            Precision = c.Precision,
            Scale = c.Scale,
            IsNullable = c.IsNullable,
            IsPrimaryKey = c.IsPrimaryKey,
            IsIdentity = c.IsIdentity,
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
            SchemaName = schemaName,
            Columns = columnsList,
            PrimaryKeys = [.. columnsList.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName)]
        };
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
                var (parentSchema, parentTable) = ParseQualifiedName(fk.TableName);
                var (referencedSchema, referencedTable) = ParseQualifiedName(fk.ReferencedTable);

                // Insert by resolving IDs from names within SQL to avoid FK issues
                await connection.ExecuteAsync(
                    SchemaSyncQueries.InsertForeignKeyMetadataByNames,
                    new
                    {
                        ProjectId = projectId,
                        ParentSchemaName = parentSchema,
                        ParentTableName = parentTable,
                        fk.ColumnName,
                        ReferencedSchemaName = referencedSchema,
                        ReferencedTable = referencedTable,
                        fk.ReferencedColumn,
                        fk.ForeignKeyName,
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
                ForeignKeyName = fk.ForeignKeyName as string,
                OnDeleteAction = (string)(fk.OnDeleteAction ?? "NO ACTION"),
                OnUpdateAction = (string)(fk.OnUpdateAction ?? "NO ACTION")
            });

            return [.. result];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting foreign keys from connection string");
            throw;
        }
    }

    public async Task<IEnumerable<IndexScanResult>> GetIndexesAsync(
        string connectionString,
        IEnumerable<string> tableNames)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithConnectionString(connectionString);

            var indexes = await connection.QueryAsync<IndexScanResult>(
                SchemaSyncQueries.GetTargetIndexesForTables,
                new { TableNames = tableNames });

            return [.. indexes];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting indexes from connection string");
            throw;
        }
    }

    private static (string SchemaName, string Name) ParseQualifiedName(string name)
    {
        var parts = name.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }

        return ("dbo", name);
    }
}

internal class StoredIndexQueryRow
{
    public int IndexId { get; set; }
    public int TableId { get; set; }
    public required string IndexName { get; set; }
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public int? ColumnId { get; set; }
    public string? ColumnName { get; set; }
    public int? ColumnOrder { get; set; }
    public bool IsIncludedColumn { get; set; }
}
