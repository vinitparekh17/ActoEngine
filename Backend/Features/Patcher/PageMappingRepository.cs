using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using Dapper;
using System.Text;

namespace ActoEngine.WebApi.Features.Patcher;

public interface IPageMappingRepository
{
    Task<MappingUpsertResult> UpsertDetectionsAsync(int projectId, IReadOnlyCollection<MappingDetectionRequest> detections, CancellationToken ct = default);
    Task<List<PageMappingDto>> GetMappingsAsync(int projectId, string? status, string? domainName, string? pageName, CancellationToken ct = default);
    Task<PageMappingDto?> GetByIdAsync(int projectId, int mappingId, CancellationToken ct = default);
    Task<bool> UpdateMappingAsync(int projectId, int mappingId, UpdateMappingRequest request, int? reviewedBy, CancellationToken ct = default);
    Task<int> BulkUpdateStatusAsync(int projectId, IReadOnlyCollection<int> ids, string action, int reviewedBy, CancellationToken ct = default);
    Task<bool> DeleteCandidateAsync(int projectId, int mappingId, CancellationToken ct = default);
    Task<List<ApprovedSpDto>> GetApprovedStoredProceduresAsync(int projectId, string domainName, string pageName, CancellationToken ct = default);
}

public class PageMappingRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<PageMappingRepository> logger) : BaseRepository(connectionFactory, logger), IPageMappingRepository
{
    public async Task<MappingUpsertResult> UpsertDetectionsAsync(int projectId, IReadOnlyCollection<MappingDetectionRequest> detections, CancellationToken ct = default)
    {
        if (detections.Count == 0)
        {
            return new MappingUpsertResult(0, 0);
        }

        // Deduplicate by natural key, keeping highest confidence per group
        var unique = detections
            .GroupBy(d => (
                DomainName: d.DomainName.Trim().ToLowerInvariant(),
                Page: d.Page.Trim().ToLowerInvariant(),
                SP: d.StoredProcedure.Trim().ToLowerInvariant(),
                Source: d.Source.Trim().ToLowerInvariant()
            ))
            .Select(g => g.OrderByDescending(d => d.Confidence ?? 0).First())
            .ToList();

        const int batchSize = 350;
        for (var i = 0; i < unique.Count; i += batchSize)
        {
            var batch = unique.Skip(i).Take(batchSize).ToList();
            var (sql, parameters) = BuildMergeSql(projectId, batch);
            await ExecuteAsync(sql, parameters, ct);
        }

        return new MappingUpsertResult(detections.Count, unique.Count);
    }

    public async Task<List<PageMappingDto>> GetMappingsAsync(
        int projectId,
        string? status,
        string? domainName,
        string? pageName,
        CancellationToken ct = default)
    {
        var (sql, parameters) = BuildGetMappingsSql(projectId, status, domainName, pageName);
        var rows = await QueryAsync<PageMappingDto>(sql, parameters, ct);
        return [.. rows];
    }

    public Task<PageMappingDto?> GetByIdAsync(int projectId, int mappingId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT MappingId, ProjectId, DomainName, PageName, StoredProcedure, Confidence,
                   Source, Status, MappingType, CreatedAt, UpdatedAt, ReviewedBy, ReviewedAt
            FROM PageMappings
            WHERE ProjectId = @ProjectId AND MappingId = @MappingId
            """;
        return QueryFirstOrDefaultAsync<PageMappingDto>(sql, new { ProjectId = projectId, MappingId = mappingId }, ct);
    }

    public async Task<bool> UpdateMappingAsync(
        int projectId,
        int mappingId,
        UpdateMappingRequest request,
        int? reviewedBy,
        CancellationToken ct = default)
    {
        var updates = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("ProjectId", projectId);
        parameters.Add("MappingId", mappingId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = PageMappingConstants.NormalizeStatus(request.Status);
            if (!PageMappingConstants.ValidStatuses.Contains(normalizedStatus))
            {
                throw new InvalidOperationException($"Invalid mapping status '{request.Status}'.");
            }

            updates.Add("Status = @Status");
            parameters.Add("Status", normalizedStatus);

            if (normalizedStatus is PageMappingConstants.StatusApproved or PageMappingConstants.StatusIgnored)
            {
                updates.Add("ReviewedBy = @ReviewedBy");
                updates.Add("ReviewedAt = GETUTCDATE()");
                parameters.Add("ReviewedBy", reviewedBy);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.MappingType))
        {
            var normalizedMappingType = PageMappingConstants.NormalizeMappingType(request.MappingType);
            if (!PageMappingConstants.ValidMappingTypes.Contains(normalizedMappingType))
            {
                throw new InvalidOperationException($"Invalid mapping type '{request.MappingType}'.");
            }

            updates.Add("MappingType = @MappingType");
            parameters.Add("MappingType", normalizedMappingType);
        }

        if (!string.IsNullOrWhiteSpace(request.DomainName))
        {
            updates.Add("DomainName = @DomainName");
            parameters.Add("DomainName", request.DomainName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.PageName))
        {
            updates.Add("PageName = @PageName");
            parameters.Add("PageName", request.PageName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.StoredProcedure))
        {
            updates.Add("StoredProcedure = @StoredProcedure");
            parameters.Add("StoredProcedure", request.StoredProcedure.Trim());
        }

        if (updates.Count == 0)
        {
            return false;
        }

        updates.Add("UpdatedAt = GETUTCDATE()");

        var sql = $"""
            UPDATE PageMappings
            SET {string.Join(", ", updates)}
            WHERE ProjectId = @ProjectId AND MappingId = @MappingId
            """;

        var rows = await ExecuteAsync(sql, parameters, ct);
        return rows > 0;
    }

    public async Task<int> BulkUpdateStatusAsync(
        int projectId,
        IReadOnlyCollection<int> ids,
        string action,
        int reviewedBy,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var normalizedAction = PageMappingConstants.NormalizeBulkAction(action);
        if (!PageMappingConstants.ValidBulkActions.Contains(normalizedAction))
        {
            throw new InvalidOperationException($"Invalid bulk action '{action}'.");
        }

        var targetStatus = normalizedAction == PageMappingConstants.BulkActionApprove
            ? PageMappingConstants.StatusApproved
            : PageMappingConstants.StatusIgnored;

        const string sql = """
            UPDATE PageMappings
            SET Status = @Status,
                ReviewedBy = @ReviewedBy,
                ReviewedAt = GETUTCDATE(),
                UpdatedAt = GETUTCDATE()
            WHERE ProjectId = @ProjectId
              AND MappingId IN @Ids
            """;

        return await ExecuteAsync(sql, new
        {
            ProjectId = projectId,
            Ids = ids,
            Status = targetStatus,
            ReviewedBy = reviewedBy
        }, ct);
    }

    public async Task<bool> DeleteCandidateAsync(int projectId, int mappingId, CancellationToken ct = default)
    {
        const string sql = """
            DELETE FROM PageMappings
            WHERE ProjectId = @ProjectId
              AND MappingId = @MappingId
              AND Status = 'candidate'
            """;

        var rows = await ExecuteAsync(sql, new { ProjectId = projectId, MappingId = mappingId }, ct);
        return rows > 0;
    }

    public async Task<List<ApprovedSpDto>> GetApprovedStoredProceduresAsync(
        int projectId,
        string domainName,
        string pageName,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT DISTINCT StoredProcedure, MappingType
            FROM PageMappings
            WHERE ProjectId = @ProjectId
              AND Status = 'approved'
              AND (
                    MappingType = 'shared'
                    OR (DomainName = @DomainName AND PageName = @PageName)
                  )
            """;

        var rows = await QueryAsync<ApprovedSpDto>(sql, new
        {
            ProjectId = projectId,
            DomainName = domainName,
            PageName = pageName
        }, ct);
        return [.. rows];
    }

    internal static (string Sql, DynamicParameters Parameters) BuildMergeSql(int projectId, IReadOnlyCollection<MappingDetectionRequest> detections)
    {
        var values = new StringBuilder();
        var parameters = new DynamicParameters();
        var i = 0;

        foreach (var detection in detections)
        {
            var source = PageMappingConstants.NormalizeSource(detection.Source);
            if (!PageMappingConstants.ValidSources.Contains(source))
            {
                throw new InvalidOperationException($"Invalid mapping source '{detection.Source}'.");
            }

            if (string.IsNullOrWhiteSpace(detection.DomainName) ||
                string.IsNullOrWhiteSpace(detection.Page) ||
                string.IsNullOrWhiteSpace(detection.StoredProcedure))
            {
                throw new InvalidOperationException("DomainName, Page and StoredProcedure are required.");
            }

            if (i > 0)
            {
                values.Append(", ");
            }

            values.Append($"(@ProjectId{i}, @DomainName{i}, @PageName{i}, @StoredProcedure{i}, @Confidence{i}, @Source{i})");

            parameters.Add($"ProjectId{i}", projectId);
            parameters.Add($"DomainName{i}", detection.DomainName.Trim());
            parameters.Add($"PageName{i}", detection.Page.Trim());
            parameters.Add($"StoredProcedure{i}", detection.StoredProcedure.Trim());
            parameters.Add($"Confidence{i}", detection.Confidence);
            parameters.Add($"Source{i}", source);

            i++;
        }

        var sql = $"""
            MERGE PageMappings WITH (HOLDLOCK) AS target
            USING (VALUES {values}) AS source (ProjectId, DomainName, PageName, StoredProcedure, Confidence, Source)
            ON target.ProjectId = source.ProjectId
               AND target.DomainName = source.DomainName
               AND target.PageName = source.PageName
               AND target.StoredProcedure = source.StoredProcedure
               AND target.Source = source.Source
            WHEN MATCHED AND target.Status = 'candidate' THEN
              UPDATE SET Confidence = source.Confidence, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
              INSERT (ProjectId, DomainName, PageName, StoredProcedure, Confidence, Source, Status, MappingType, CreatedAt, UpdatedAt)
              VALUES (source.ProjectId, source.DomainName, source.PageName, source.StoredProcedure, source.Confidence, source.Source, 'candidate', 'page_specific', GETUTCDATE(), GETUTCDATE());
            """;

        return (sql, parameters);
    }

    internal static (string Sql, DynamicParameters Parameters) BuildGetMappingsSql(
        int projectId,
        string? status,
        string? domainName,
        string? pageName)
    {
        var sql = new StringBuilder("""
            SELECT MappingId, ProjectId, DomainName, PageName, StoredProcedure, Confidence,
                   Source, Status, MappingType, CreatedAt, UpdatedAt, ReviewedBy, ReviewedAt
            FROM PageMappings
            WHERE ProjectId = @ProjectId
            """);

        var parameters = new DynamicParameters();
        parameters.Add("ProjectId", projectId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = PageMappingConstants.NormalizeStatus(status);
            if (!PageMappingConstants.ValidStatuses.Contains(normalizedStatus))
            {
                throw new InvalidOperationException($"Invalid mapping status filter '{status}'.");
            }

            sql.Append(" AND Status = @Status");
            parameters.Add("Status", normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(domainName))
        {
            sql.Append(" AND DomainName = @DomainName");
            parameters.Add("DomainName", domainName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(pageName))
        {
            sql.Append(" AND PageName = @PageName");
            parameters.Add("PageName", pageName.Trim());
        }

        sql.Append(" ORDER BY DomainName, PageName, StoredProcedure");
        return (sql.ToString(), parameters);
    }
}
