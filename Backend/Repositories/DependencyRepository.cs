using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using Dapper;
using System.Data;

namespace ActoEngine.WebApi.Repositories;

/// <summary>
/// Repository contract for Dependency operations
/// </summary>
public interface IDependencyRepository
{
    /// <summary>
    /// Saves dependencies for specified sources in a transaction.
    /// Deletes existing dependencies for the sources and inserts new ones atomically.
    /// <summary>
/// Saves the provided dependencies for the specified project and sources in a single transaction, removing existing dependencies for those sources and inserting the new records atomically.
/// </summary>
/// <param name="projectId">The identifier of the project these dependencies belong to.</param>
/// <param name="dependencies">The collection of dependencies to persist; each item identifies a source and its target dependency.</param>
/// <param name="cancellationToken">Token to observe for cancellation of the operation.</param>
    Task SaveDependenciesForSourcesAsync(int projectId, IEnumerable<ResolvedDependency> dependencies, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for Dependency operations
/// </summary>
public class DependencyRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<DependencyRepository> logger) : BaseRepository(connectionFactory, logger), IDependencyRepository
{
    /// <summary>
    /// Persists the given dependencies for a project by replacing existing dependencies for the same sources within a single database transaction.
    /// </summary>
    /// <param name="projectId">The project identifier to which the dependencies belong.</param>
    /// <param name="dependencies">The collection of dependencies to save; if empty, no changes are made.</param>
    /// <param name="cancellationToken">Token to observe for request cancellation.</param>
    public async Task SaveDependenciesForSourcesAsync(
        int projectId,
        IEnumerable<ResolvedDependency> dependencies,
        CancellationToken cancellationToken = default)
    {
        var depsList = dependencies.ToList();
        if (depsList.Count == 0) return;

        await ExecuteInTransactionAsync(async (conn, transaction) =>
        {
            // 1. Clean up existing dependencies for these sources (to avoid duplicates on re-sync)
            var sourceIds = depsList.Select(d => d.SourceId).Distinct().ToList();
            var sourceTypes = depsList.Select(d => d.SourceType).Distinct().ToList();

            var deleteSql = @"
                DELETE FROM Dependencies
                WHERE ProjectId = @ProjectId
                  AND SourceId IN @SourceIds
                  AND SourceType IN @SourceTypes";

            var deleteParams = new
            {
                ProjectId = projectId,
                SourceIds = sourceIds,
                SourceTypes = sourceTypes
            };

            await conn.ExecuteAsync(deleteSql, deleteParams, transaction);

            // 2. Insert new dependencies
            var insertSql = @"
                INSERT INTO Dependencies (ProjectId, SourceType, SourceId, TargetType, TargetId, DependencyType, ConfidenceScore)
                VALUES (@ProjectId, @SourceType, @SourceId, @TargetType, @TargetId, @DependencyType, @ConfidenceScore)";

            await conn.ExecuteAsync(insertSql, depsList, transaction);

            _logger.LogInformation(
                "Saved {Count} dependencies for project {ProjectId} (sources: {SourceCount})",
                depsList.Count,
                projectId,
                sourceIds.Count);

            return true; // Return value required by ExecuteInTransactionAsync
        }, cancellationToken);
    }
}

/// <summary>
/// Resolved dependency model for database operations
/// </summary>
public class ResolvedDependency
{
    public int ProjectId { get; set; }
    public required string SourceType { get; set; }
    public int SourceId { get; set; }
    public required string TargetType { get; set; }
    public int TargetId { get; set; }
    public required string DependencyType { get; set; }
    public decimal ConfidenceScore { get; set; }
}