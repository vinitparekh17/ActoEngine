using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using ActoEngine.WebApi.Features.Schema;
using Dapper;
using System.Data;
using System.Text;

namespace ActoEngine.WebApi.Features.Patcher;

public interface IPatcherRepository
{
    Task<ProjectPatchConfig?> GetPatchConfigAsync(int projectId, CancellationToken ct = default);
    Task UpdatePatchConfigAsync(int projectId, PatchConfigRequest config, CancellationToken ct = default);
    Task<PatchHistoryRecord?> GetLatestPatchAsync(int projectId, string domainName, string pageName, CancellationToken ct = default);
    Task<Dictionary<string, PatchHistoryRecord>> GetLatestPatchesAsync(int projectId, IReadOnlyCollection<PatchPageEntry> pages, CancellationToken ct = default);
    Task<List<PatchHistoryRecord>> GetPatchHistoryAsync(int projectId, int offset = 0, int limit = 50, CancellationToken ct = default);
    Task<PatchHistoryPageResponse> GetPatchHistoryPagedAsync(int projectId, int page, int pageSize, CancellationToken ct = default);
    Task<int> SavePatchHistoryAsync(PatchHistoryRecord record, List<PatchPageEntry> pages, CancellationToken ct = default);
    Task<PatchHistoryRecord?> GetPatchByIdAsync(int patchId, int userId, CancellationToken ct = default);
    Task<List<SpTableDependencyRow>> GetSpOutboundDependenciesAsync(int projectId, int spId, CancellationToken ct = default);
    Task<List<SpProcedureDependencyRow>> GetSpProcedureDependenciesAsync(int projectId, int spId, CancellationToken ct = default);
    Task<List<SpColumnDependencyRow>> GetSpColumnDependenciesAsync(int projectId, int spId, CancellationToken ct = default);
    Task<Dictionary<int, List<SpTableDependencyRow>>> GetSpOutboundDependenciesAsync(int projectId, IReadOnlyCollection<int> spIds, CancellationToken ct = default);
    Task<Dictionary<int, List<SpProcedureDependencyRow>>> GetSpProcedureDependenciesAsync(int projectId, IReadOnlyCollection<int> spIds, CancellationToken ct = default);
    Task<Dictionary<int, List<SpColumnDependencyRow>>> GetSpColumnDependenciesAsync(int projectId, IReadOnlyCollection<int> spIds, CancellationToken ct = default);
    Task<Dictionary<int, StoredProcedureMetadataDto>> GetStoredProceduresByIdsAsync(IReadOnlyCollection<int> spIds, CancellationToken ct = default);
    Task<Dictionary<int, TableMetadataDto>> GetTablesByIdsAsync(IReadOnlyCollection<int> tableIds, CancellationToken ct = default);
    Task<Dictionary<int, List<ColumnMetadataDto>>> GetColumnsByTableIdsAsync(IReadOnlyCollection<int> tableIds, CancellationToken ct = default);
    Task<Dictionary<int, List<StoredIndexDto>>> GetIndexesByTableIdsAsync(IReadOnlyCollection<int> tableIds, CancellationToken ct = default);
    Task<Dictionary<int, List<StoredForeignKeyDto>>> GetForeignKeysByTableIdsAsync(IReadOnlyCollection<int> tableIds, CancellationToken ct = default);
}

public class PatcherRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<PatcherRepository> logger) : BaseRepository(connectionFactory, logger), IPatcherRepository
{
    public async Task<ProjectPatchConfig?> GetPatchConfigAsync(int projectId, CancellationToken ct = default)
    {
        return await QueryFirstOrDefaultAsync<ProjectPatchConfig>(
            PatcherQueries.GetPatchConfig,
            new { ProjectId = projectId }, ct);
    }

    public async Task UpdatePatchConfigAsync(int projectId, PatchConfigRequest config, CancellationToken ct = default)
    {
        await ExecuteAsync(
            PatcherQueries.UpdatePatchConfig,
            new
            {
                ProjectId = projectId,
                config.ProjectRootPath,
                config.ViewDirPath,
                config.ScriptDirPath,
                config.PatchDownloadPath
            }, ct);
    }

    public async Task<PatchHistoryRecord?> GetLatestPatchAsync(
        int projectId, string domainName, string pageName, CancellationToken ct = default)
    {
        return await QueryFirstOrDefaultAsync<PatchHistoryRecord>(
            PatcherQueries.GetLatestPatch,
            new { ProjectId = projectId, DomainName = domainName, PageName = pageName }, ct);
    }

    public async Task<Dictionary<string, PatchHistoryRecord>> GetLatestPatchesAsync(
        int projectId,
        IReadOnlyCollection<PatchPageEntry> pages,
        CancellationToken ct = default)
    {
        var normalized = NormalizePageEntries(pages);
        if (normalized.Count == 0)
        {
            return new Dictionary<string, PatchHistoryRecord>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new StringBuilder();
        var parameters = new DynamicParameters();
        parameters.Add("ProjectId", projectId);

        for (var i = 0; i < normalized.Count; i++)
        {
            if (i > 0)
            {
                values.Append(", ");
            }

            values.Append($"(@Domain{i}, @Page{i})");
            parameters.Add($"Domain{i}", normalized[i].DomainName);
            parameters.Add($"Page{i}", normalized[i].PageName);
        }

        var sql = $@"
            WITH Requested(DomainName, PageName) AS (
                SELECT v.DomainName, v.PageName
                FROM (VALUES {values}) v(DomainName, PageName)
            )
            SELECT
                r.DomainName AS RequestDomainName,
                r.PageName AS RequestPageName,
                ph.PatchId,
                ph.ProjectId,
                ph.PageName,
                ph.DomainName,
                ph.SpNames,
                ph.PatchName,
                ph.IsNewPage,
                ph.PatchFilePath,
                ph.PatchSignature,
                ph.GeneratedAt,
                ph.GeneratedBy,
                ph.Status
            FROM Requested r
            OUTER APPLY (
                SELECT TOP 1
                    h.PatchId,
                    h.ProjectId,
                    h.PageName,
                    h.DomainName,
                    h.SpNames,
                    h.PatchName,
                    h.IsNewPage,
                    h.PatchFilePath,
                    h.PatchSignature,
                    h.GeneratedAt,
                    h.GeneratedBy,
                    h.Status
                FROM PatchHistory h
                WHERE h.ProjectId = @ProjectId
                  AND EXISTS (
                      SELECT 1
                      FROM PatchHistoryPages php
                      WHERE php.PatchId = h.PatchId
                        AND php.DomainName = r.DomainName
                        AND php.PageName = r.PageName
                  )
                ORDER BY h.GeneratedAt DESC, h.PatchId DESC
            ) ph
            WHERE ph.PatchId IS NOT NULL";

        var rows = await QueryAsync<LatestPatchByPageFlatRow>(sql, parameters, ct);
        return rows.ToDictionary(
            row => BuildPageKey(row.RequestDomainName, row.RequestPageName),
            row => new PatchHistoryRecord
            {
                PatchId = row.PatchId,
                ProjectId = row.ProjectId,
                PageName = row.PageName,
                DomainName = row.DomainName,
                SpNames = row.SpNames,
                PatchName = row.PatchName,
                IsNewPage = row.IsNewPage,
                PatchFilePath = row.PatchFilePath,
                PatchSignature = row.PatchSignature,
                GeneratedAt = row.GeneratedAt,
                GeneratedBy = row.GeneratedBy,
                Status = row.Status
            },
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<PatchHistoryRecord>> GetPatchHistoryAsync(int projectId, int offset = 0, int limit = 50, CancellationToken ct = default)
    {
        var safeOffset = Math.Max(0, offset);
        var safeLimit = Math.Clamp(limit, 1, 500);
        var records = (await QueryAsync<PatchHistoryRecord>(
            PatcherQueries.GetPatchHistoryPaged,
            new { ProjectId = projectId, Offset = safeOffset, Limit = safeLimit }, ct)).ToList();

        await AttachPagesAsync(records, ct);
        return records;
    }

    public async Task<PatchHistoryPageResponse> GetPatchHistoryPagedAsync(
        int projectId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 1, 200);
        int offset = (safePage - 1) * safePageSize;

        List<PatchHistoryRecord> items = (await QueryAsync<PatchHistoryRecord>(
            PatcherQueries.GetPatchHistoryPaged,
            new { ProjectId = projectId, Offset = offset, Limit = safePageSize }, ct)).ToList();

        await AttachPagesAsync(items, ct);

        int total = await ExecuteScalarAsync<int>(
            PatcherQueries.GetPatchHistoryCount,
            new { ProjectId = projectId },
            ct);

        return new PatchHistoryPageResponse
        {
            Items = items,
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = total
        };
    }

    public async Task<int> SavePatchHistoryAsync(PatchHistoryRecord record, List<PatchPageEntry> pages, CancellationToken ct = default)
    {
        return await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            CommandDefinition insertCmd = new CommandDefinition(
                PatcherQueries.InsertPatchHistory,
                new
                {
                    record.ProjectId,
                    record.PageName,
                    record.DomainName,
                    record.SpNames,
                    record.PatchName,
                    record.IsNewPage,
                    record.PatchFilePath,
                    record.PatchSignature,
                    record.GeneratedBy,
                    record.Status
                },
                transaction,
                cancellationToken: ct);

            int patchId = await connection.QuerySingleAsync<int>(insertCmd);

            if (pages.Count > 0)
            {
                var pageParams = pages.Select(page => new
                {
                    PatchId = patchId,
                    DomainName = page.DomainName,
                    PageName = page.PageName,
                    IsNewPage = page.IsNewPage
                }).ToList();

                CommandDefinition pageCmd = new CommandDefinition(
                    PatcherQueries.InsertPatchHistoryPages,
                    pageParams,
                    transaction,
                    cancellationToken: ct);

                await connection.ExecuteAsync(pageCmd);
            }

            return patchId;
        }, ct);
    }

    public async Task<PatchHistoryRecord?> GetPatchByIdAsync(int patchId, int userId, CancellationToken ct = default)
    {
        return await QueryFirstOrDefaultAsync<PatchHistoryRecord>(
            PatcherQueries.GetPatchById,
            new { PatchId = patchId, UserId = userId }, ct);
    }

    /// <summary>
    /// Gets the tables that a given SP depends on (outbound dependencies from SP).
    /// Dependencies table: SourceType=SP, SourceId=spId → TargetType=TABLE
    /// </summary>
    public async Task<List<SpTableDependencyRow>> GetSpOutboundDependenciesAsync(
        int projectId, int spId, CancellationToken ct = default)
    {
        var batch = await GetSpOutboundDependenciesAsync(projectId, [spId], ct);
        return batch.TryGetValue(spId, out var dependencies) ? dependencies : [];
    }

    public async Task<List<SpProcedureDependencyRow>> GetSpProcedureDependenciesAsync(
        int projectId, int spId, CancellationToken ct = default)
    {
        var batch = await GetSpProcedureDependenciesAsync(projectId, [spId], ct);
        return batch.TryGetValue(spId, out var dependencies) ? dependencies : [];
    }

    public async Task<List<SpColumnDependencyRow>> GetSpColumnDependenciesAsync(
        int projectId, int spId, CancellationToken ct = default)
    {
        var batch = await GetSpColumnDependenciesAsync(projectId, [spId], ct);
        return batch.TryGetValue(spId, out var dependencies) ? dependencies : [];
    }

    public async Task<Dictionary<int, List<SpTableDependencyRow>>> GetSpOutboundDependenciesAsync(
        int projectId,
        IReadOnlyCollection<int> spIds,
        CancellationToken ct = default)
    {
        var ids = NormalizeIds(spIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await QueryAsync<SpTableDependencyBatchRow>(
            PatcherQueries.GetSpOutboundDependenciesBatch,
            new { ProjectId = projectId, SpIds = ids },
            ct);

        return rows.GroupBy(row => row.SourceSpId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new SpTableDependencyRow
                {
                    TableId = row.TableId,
                    TableName = row.TableName,
                    SchemaName = row.SchemaName
                }).ToList());
    }

    public async Task<Dictionary<int, List<SpProcedureDependencyRow>>> GetSpProcedureDependenciesAsync(
        int projectId,
        IReadOnlyCollection<int> spIds,
        CancellationToken ct = default)
    {
        var ids = NormalizeIds(spIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await QueryAsync<SpProcedureDependencyBatchRow>(
            PatcherQueries.GetSpProcedureDependenciesBatch,
            new { ProjectId = projectId, SpIds = ids },
            ct);

        return rows.GroupBy(row => row.SourceSpId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new SpProcedureDependencyRow
                {
                    SpId = row.SpId,
                    ProcedureName = row.ProcedureName,
                    SchemaName = row.SchemaName
                }).ToList());
    }

    public async Task<Dictionary<int, List<SpColumnDependencyRow>>> GetSpColumnDependenciesAsync(
        int projectId,
        IReadOnlyCollection<int> spIds,
        CancellationToken ct = default)
    {
        var ids = NormalizeIds(spIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await QueryAsync<SpColumnDependencyBatchRow>(
            PatcherQueries.GetSpColumnDependenciesBatch,
            new { ProjectId = projectId, SpIds = ids },
            ct);

        return rows.GroupBy(row => row.SourceSpId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new SpColumnDependencyRow
                {
                    ColumnId = row.ColumnId,
                    ColumnName = row.ColumnName,
                    TableId = row.TableId,
                    TableName = row.TableName,
                    SchemaName = row.SchemaName
                }).ToList());
    }

    public async Task<Dictionary<int, StoredProcedureMetadataDto>> GetStoredProceduresByIdsAsync(
        IReadOnlyCollection<int> spIds,
        CancellationToken ct = default)
    {
        var ids = NormalizeIds(spIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var procedures = await QueryAsync<StoredProcedureMetadataDto>(
            PatcherQueries.GetStoredProceduresByIds,
            new { SpIds = ids },
            ct);

        return procedures.ToDictionary(sp => sp.SpId);
    }

    public async Task<Dictionary<int, TableMetadataDto>> GetTablesByIdsAsync(
        IReadOnlyCollection<int> tableIds,
        CancellationToken ct = default)
    {
        var ids = NormalizeIds(tableIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var tables = await QueryAsync<TableMetadataDto>(
            PatcherQueries.GetTablesByIds,
            new { TableIds = ids },
            ct);

        return tables.ToDictionary(table => table.TableId);
    }

    public async Task<Dictionary<int, List<ColumnMetadataDto>>> GetColumnsByTableIdsAsync(
        IReadOnlyCollection<int> tableIds,
        CancellationToken ct = default)
    {
        var ids = NormalizeIds(tableIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await QueryAsync<ColumnMetadataDto>(
            PatcherQueries.GetColumnsByTableIds,
            new { TableIds = ids },
            ct);

        return rows.GroupBy(row => row.TableId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public async Task<Dictionary<int, List<StoredIndexDto>>> GetIndexesByTableIdsAsync(
        IReadOnlyCollection<int> tableIds,
        CancellationToken ct = default)
    {
        var ids = NormalizeIds(tableIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = (await QueryAsync<StoredIndexQueryRow>(
            PatcherQueries.GetIndexesByTableIds,
            new { TableIds = ids },
            ct)).ToList();

        return rows.GroupBy(row => row.TableId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(row => new { row.IndexId, row.TableId, row.IndexName, row.IsUnique, row.IsPrimaryKey })
                    .Select(indexGroup => new StoredIndexDto
                    {
                        IndexId = indexGroup.Key.IndexId,
                        TableId = indexGroup.Key.TableId,
                        IndexName = indexGroup.Key.IndexName,
                        IsUnique = indexGroup.Key.IsUnique,
                        IsPrimaryKey = indexGroup.Key.IsPrimaryKey,
                        Columns = indexGroup
                            .Where(row => row.ColumnId.HasValue && row.ColumnName != null)
                            .Select(row => new StoredIndexColumnDto
                            {
                                ColumnId = row.ColumnId!.Value,
                                ColumnName = row.ColumnName!,
                                ColumnOrder = row.ColumnOrder ?? 0,
                                IsIncludedColumn = row.IsIncludedColumn
                            })
                            .OrderBy(column => column.ColumnOrder)
                            .ToList()
                    }).ToList());
    }

    public async Task<Dictionary<int, List<StoredForeignKeyDto>>> GetForeignKeysByTableIdsAsync(
        IReadOnlyCollection<int> tableIds,
        CancellationToken ct = default)
    {
        var ids = NormalizeIds(tableIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = (await QueryAsync<StoredForeignKeyDto>(
            PatcherQueries.GetForeignKeysByTableIds,
            new { TableIds = ids },
            ct)).ToList();

        return rows.GroupBy(row => row.TableId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private async Task AttachPagesAsync(List<PatchHistoryRecord> records, CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return;
        }

        var patchIds = records.Select(record => record.PatchId).Distinct().ToList();
        var pageRows = (await QueryAsync<PatchHistoryPageFlatRow>(
            PatcherQueries.GetPatchHistoryPagesByPatchIds,
            new { PatchIds = patchIds },
            ct)).ToList();

        var pagesByPatchId = pageRows.GroupBy(row => row.PatchId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new PatchPageEntry
                {
                    DomainName = row.DomainName,
                    PageName = row.PageName,
                    IsNewPage = row.IsNewPage
                }).ToList());

        foreach (var record in records)
        {
            if (pagesByPatchId.TryGetValue(record.PatchId, out var pages))
            {
                record.Pages = pages;
            }
            else
            {
                record.Pages = [];
            }
        }
    }

    private static List<PatchPageEntry> NormalizePageEntries(IReadOnlyCollection<PatchPageEntry> pages)
    {
        return [.. pages
            .Where(page => !string.IsNullOrWhiteSpace(page.DomainName) && !string.IsNullOrWhiteSpace(page.PageName))
            .GroupBy(page => BuildPageKey(page.DomainName, page.PageName), StringComparer.OrdinalIgnoreCase)
            .Select(group => new PatchPageEntry
            {
                DomainName = group.First().DomainName.Trim(),
                PageName = group.First().PageName.Trim(),
                IsNewPage = group.Any(page => page.IsNewPage)
            })];
    }

    internal static string BuildPageKey(string domainName, string pageName)
    {
        return $"{domainName.Trim().ToLowerInvariant()}::{pageName.Trim().ToLowerInvariant()}";
    }

    private static List<int> NormalizeIds(IReadOnlyCollection<int> ids)
    {
        return [.. ids.Where(id => id > 0).Distinct().OrderBy(id => id)];
    }
}

public class SpTableDependencyRow
{
    public int TableId { get; set; }
    public required string TableName { get; set; }
    public string? SchemaName { get; set; }
}

public class SpProcedureDependencyRow
{
    public int SpId { get; set; }
    public required string ProcedureName { get; set; }
    public string? SchemaName { get; set; }
}

public class SpColumnDependencyRow
{
    public int ColumnId { get; set; }
    public required string ColumnName { get; set; }
    public int TableId { get; set; }
    public required string TableName { get; set; }
    public string? SchemaName { get; set; }
}

internal class SpTableDependencyBatchRow
{
    public int SourceSpId { get; set; }
    public int TableId { get; set; }
    public required string TableName { get; set; }
    public string? SchemaName { get; set; }
}

internal class SpProcedureDependencyBatchRow
{
    public int SourceSpId { get; set; }
    public int SpId { get; set; }
    public required string ProcedureName { get; set; }
    public string? SchemaName { get; set; }
}

internal class SpColumnDependencyBatchRow
{
    public int SourceSpId { get; set; }
    public int ColumnId { get; set; }
    public required string ColumnName { get; set; }
    public int TableId { get; set; }
    public required string TableName { get; set; }
    public string? SchemaName { get; set; }
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
