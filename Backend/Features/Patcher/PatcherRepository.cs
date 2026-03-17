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
            @"SELECT ProjectRootPath, ViewDirPath, ScriptDirPath, PatchDownloadPath
              FROM Projects
              WHERE ProjectId = @ProjectId",
            new { ProjectId = projectId }, ct);
    }

    public async Task UpdatePatchConfigAsync(int projectId, PatchConfigRequest config, CancellationToken ct = default)
    {
        await ExecuteAsync(
            @"UPDATE Projects
              SET ProjectRootPath = @ProjectRootPath,
                  ViewDirPath = @ViewDirPath,
                  ScriptDirPath = @ScriptDirPath,
                  PatchDownloadPath = @PatchDownloadPath
              WHERE ProjectId = @ProjectId",
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
            @"SELECT TOP 1 ph.PatchId, ph.ProjectId, ph.PageName, ph.DomainName, ph.SpNames,
                           ph.IsNewPage, ph.PatchFilePath, ph.GeneratedAt, ph.GeneratedBy, ph.Status
              FROM PatchHistory ph
              JOIN PatchHistoryPages php ON ph.PatchId = php.PatchId
              WHERE ph.ProjectId = @ProjectId
                AND php.DomainName = @DomainName
                AND php.PageName = @PageName
              ORDER BY ph.GeneratedAt DESC",
            new { ProjectId = projectId, DomainName = domainName, PageName = pageName }, ct);
    }

    public async Task<List<PatchHistoryRecord>> GetPatchHistoryAsync(int projectId, CancellationToken ct = default)
    {
        var flat = await QueryAsync<PatchHistoryFlatRow>(
            @"SELECT ph.PatchId, ph.ProjectId, ph.PageName, ph.DomainName, ph.SpNames,
                     ph.IsNewPage, ph.PatchFilePath, ph.GeneratedAt, ph.GeneratedBy, ph.Status,
                     php.DomainName AS PageDomain, php.PageName AS PagePage, php.IsNewPage AS PageIsNew
              FROM PatchHistory ph
              LEFT JOIN PatchHistoryPages php ON ph.PatchId = php.PatchId
              WHERE ph.ProjectId = @ProjectId
              ORDER BY ph.GeneratedAt DESC",
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
                @"INSERT INTO PatchHistory
                      (ProjectId, PageName, DomainName, SpNames, IsNewPage, PatchFilePath, GeneratedAt, GeneratedBy, Status)
                  VALUES
                      (@ProjectId, @PageName, @DomainName, @SpNames, @IsNewPage, @PatchFilePath, GETUTCDATE(), @GeneratedBy, @Status);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    record.ProjectId,
                    record.PageName,
                    record.DomainName,
                    record.SpNames,
                    record.IsNewPage,
                    record.PatchFilePath,
                    record.GeneratedBy,
                    record.Status
                },
                transaction);

            await connection.ExecuteAsync(
                @"INSERT INTO PatchHistoryPages (PatchId, DomainName, PageName, IsNewPage)
                  VALUES (@PatchId, @DomainName, @PageName, @IsNewPage)",
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
            @"SELECT PatchId, ProjectId, PageName, DomainName, SpNames,
                     IsNewPage, PatchFilePath, GeneratedAt, GeneratedBy, Status
              FROM PatchHistory
              WHERE PatchId = @PatchId",
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
            @"SELECT d.TargetId AS TableId, t.TableName, t.SchemaName
              FROM Dependencies d
              JOIN TablesMetadata t ON d.TargetId = t.TableId AND t.ProjectId = d.ProjectId AND t.IsDeleted = 0
              WHERE d.ProjectId = @ProjectId
                AND d.SourceType = 'SP'
                AND d.SourceId = @SpId
                AND d.TargetType = 'TABLE'",
            new { ProjectId = projectId, SpId = spId }, ct);

        return [.. results];
    }

    public async Task<List<SpProcedureDependencyRow>> GetSpProcedureDependenciesAsync(
        int projectId, int spId, CancellationToken ct = default)
    {
        var results = await QueryAsync<SpProcedureDependencyRow>(
            @"SELECT d.TargetId AS SpId, s.ProcedureName, s.SchemaName
              FROM Dependencies d
              JOIN SpMetadata s ON d.TargetId = s.SpId AND s.ProjectId = d.ProjectId AND s.IsDeleted = 0
              WHERE d.ProjectId = @ProjectId
                AND d.SourceType = 'SP'
                AND d.SourceId = @SpId
                AND d.TargetType = 'SP'",
            new { ProjectId = projectId, SpId = spId }, ct);

        return [.. results];
    }

    public async Task<List<SpColumnDependencyRow>> GetSpColumnDependenciesAsync(
        int projectId, int spId, CancellationToken ct = default)
    {
        var results = await QueryAsync<SpColumnDependencyRow>(
            @"SELECT
                    d.TargetId AS ColumnId,
                    c.ColumnName,
                    t.TableId,
                    t.TableName,
                    t.SchemaName
              FROM Dependencies d
              JOIN ColumnsMetadata c ON d.TargetId = c.ColumnId
              JOIN TablesMetadata t ON c.TableId = t.TableId AND t.ProjectId = d.ProjectId AND t.IsDeleted = 0
              WHERE d.ProjectId = @ProjectId
                AND d.SourceType = 'SP'
                AND d.SourceId = @SpId
                AND d.TargetType = 'COLUMN'",
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
