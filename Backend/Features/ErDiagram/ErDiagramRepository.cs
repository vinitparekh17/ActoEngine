
using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using Dapper;

namespace ActoEngine.WebApi.Features.ErDiagram;

public interface IErDiagramRepository
{
    Task<TableInfo?> GetTableInfoAsync(int projectId, int tableId);
    Task<List<RawFkEdge>> GetPhysicalFksAsync(int projectId);
    Task<List<RawLogicalFkEdge>> GetLogicalFksAsync(int projectId);
    Task<List<TableInfo>> GetTablesByIdsAsync(IEnumerable<int> tableIds);
    Task<List<ColumnInfo>> GetColumnsByTableIdsAsync(IEnumerable<int> tableIds);
}

public class ErDiagramRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ErDiagramRepository> logger)
    : BaseRepository(connectionFactory, logger), IErDiagramRepository
{
    public async Task<TableInfo?> GetTableInfoAsync(int projectId, int tableId)
    {
        return await QueryFirstOrDefaultAsync<TableInfo>(
            ErDiagramQueries.GetTableInfo,
            new { ProjectId = projectId, TableId = tableId });
    }

    public async Task<List<RawFkEdge>> GetPhysicalFksAsync(int projectId)
    {
        var result = await QueryAsync<RawFkEdge>(
            ErDiagramQueries.GetPhysicalFks,
            new { ProjectId = projectId });
        return [.. result];
    }

    public async Task<List<RawLogicalFkEdge>> GetLogicalFksAsync(int projectId)
    {
        try
        {
            var result = await QueryAsync<RawLogicalFkEdge>(
                ErDiagramQueries.GetLogicalFks,
                new { ProjectId = projectId });
            return [.. result];
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            if (ex.Number == 208) // Invalid object name
            {
                _logger.LogWarning("LogicalForeignKeys table not found — skipping logical FK edges.");
                return [];
            }
            throw;
        }
    }

    public async Task<List<TableInfo>> GetTablesByIdsAsync(IEnumerable<int> tableIds)
    {
        var result = await QueryAsync<TableInfo>(
            ErDiagramQueries.GetTablesByIds,
            new { TableIds = tableIds });
        return [.. result];
    }

    public async Task<List<ColumnInfo>> GetColumnsByTableIdsAsync(IEnumerable<int> tableIds)
    {
        var result = await QueryAsync<ColumnInfo>(
            ErDiagramQueries.GetColumnsByTableIds,
            new { TableIds = tableIds });
        return [.. result];
    }
}
