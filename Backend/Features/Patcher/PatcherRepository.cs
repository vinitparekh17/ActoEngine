using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using Dapper;
using System.Data;

namespace ActoEngine.WebApi.Features.Patcher;

public interface IPatcherRepository
{
    Task<ProjectPatchConfig?> GetPatchConfigAsync(int projectId, CancellationToken ct = default);
    Task UpdatePatchConfigAsync(int projectId, PatchConfigRequest config, CancellationToken ct = default);
    Task<PatchHistoryRecord?> GetLatestPatchAsync(int projectId, string domainName, string pageName, CancellationToken ct = default);
    Task<List<PatchHistoryRecord>> GetPatchHistoryAsync(int projectId, CancellationToken ct = default);
    Task<int> SavePatchHistoryAsync(PatchHistoryRecord record, List<PatchPageEntry> pages, CancellationToken ct = default);
    Task<PatchHistoryRecord?> GetPatchByIdAsync(int patchId, CancellationToken ct = default);
    Task<List<SpTableDependencyRow>> GetSpOutboundDependenciesAsync(int projectId, int spId, CancellationToken ct = default);
    Task<List<SpProcedureDependencyRow>> GetSpProcedureDependenciesAsync(int projectId, int spId, CancellationToken ct = default);
    Task<List<SpColumnDependencyRow>> GetSpColumnDependenciesAsync(int projectId, int spId, CancellationToken ct = default);
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

    public async Task<List<PatchHistoryRecord>> GetPatchHistoryAsync(int projectId, CancellationToken ct = default)
    {
        var flat = await QueryAsync<PatchHistoryFlatRow>(
            PatcherQueries.GetPatchHistory,
            new { ProjectId = projectId }, ct);

        return flat.GroupBy(r => r.PatchId)
            .Select(g =>
            {
                var first = g.First();
                return new PatchHistoryRecord
                {
                    PatchId = first.PatchId,
                    ProjectId = first.ProjectId,
                    PageName = first.PageName,
                    DomainName = first.DomainName,
                    SpNames = first.SpNames,
                    PatchName = first.PatchName,
                    IsNewPage = first.IsNewPage,
                    PatchFilePath = first.PatchFilePath,
                    GeneratedAt = first.GeneratedAt,
                    GeneratedBy = first.GeneratedBy,
                    Status = first.Status,
                    Pages = g.Where(r => r.PageDomain != null)
                             .Select(r => new PatchPageEntry
                             {
                                 DomainName = r.PageDomain!,
                                 PageName = r.PagePage!,
                                 IsNewPage = r.PageIsNew
                             }).ToList()
                };
            }).ToList();
    }

    public async Task<int> SavePatchHistoryAsync(PatchHistoryRecord record, List<PatchPageEntry> pages, CancellationToken ct = default)
    {
        return await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var patchId = await connection.QuerySingleAsync<int>(
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
                    record.GeneratedBy,
                    record.Status
                },
                transaction);

            await connection.ExecuteAsync(
                PatcherQueries.InsertPatchHistoryPages,
                pages.Select(p => new
                {
                    PatchId = patchId,
                    p.DomainName,
                    p.PageName,
                    p.IsNewPage
                }),
                transaction);

            return patchId;
        }, ct);
    }

    public async Task<PatchHistoryRecord?> GetPatchByIdAsync(int patchId, CancellationToken ct = default)
    {
        return await QueryFirstOrDefaultAsync<PatchHistoryRecord>(
            PatcherQueries.GetPatchById,
            new { PatchId = patchId }, ct);
    }

    /// <summary>
    /// Gets the tables that a given SP depends on (outbound dependencies from SP).
    /// Dependencies table: SourceType=SP, SourceId=spId → TargetType=TABLE
    /// </summary>
    public async Task<List<SpTableDependencyRow>> GetSpOutboundDependenciesAsync(
        int projectId, int spId, CancellationToken ct = default)
    {
        var results = await QueryAsync<SpTableDependencyRow>(
            PatcherQueries.GetSpOutboundDependencies,
            new { ProjectId = projectId, SpId = spId }, ct);

        return [.. results];
    }

    public async Task<List<SpProcedureDependencyRow>> GetSpProcedureDependenciesAsync(
        int projectId, int spId, CancellationToken ct = default)
    {
        var results = await QueryAsync<SpProcedureDependencyRow>(
            PatcherQueries.GetSpProcedureDependencies,
            new { ProjectId = projectId, SpId = spId }, ct);

        return [.. results];
    }

    public async Task<List<SpColumnDependencyRow>> GetSpColumnDependenciesAsync(
        int projectId, int spId, CancellationToken ct = default)
    {
        var results = await QueryAsync<SpColumnDependencyRow>(
            PatcherQueries.GetSpColumnDependencies,
            new { ProjectId = projectId, SpId = spId }, ct);

        return [.. results];
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
