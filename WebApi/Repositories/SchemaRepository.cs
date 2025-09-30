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
            foreach (var sp in procedures)
            {
                await connection.ExecuteAsync(
                    SchemaSyncQueries.InsertSpMetadata,
                    new
                    {
                        ProjectId = projectId,
                        ClientId = clientId,
                        sp.ProcedureName,
                        sp.Definition,
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
            const string query = "SELECT TableId, TableName FROM TablesMetadata WHERE ProjectId = @ProjectId";
            return await connection.QueryAsync<(int, string)>(query, new { ProjectId = projectId }, transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tables for project {ProjectId}", projectId);
            throw;
        }
    }
}