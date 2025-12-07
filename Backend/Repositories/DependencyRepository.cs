using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.SqlQueries;
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
    /// </summary>
    Task SaveDependenciesForSourcesAsync(int projectId, IEnumerable<ResolvedDependency> dependencies, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dependents for a specific entity
    /// </summary>
    Task<List<ResolvedDependency>> GetDependentsAsync(int projectId, string targetType, int targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves downstream dependent nodes in the dependency graph
    /// for a given project and root entity
    /// </summary>
    /// <param name="projectId">The project identifier</param>
    /// <param name="rootType">The root entity type (TABLE, SP, VIEW)</param>
    /// <param name="rootId">The root entity identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Task containing a list of DependencyGraphNode representing downstream dependents</returns>
    Task<List<DependencyGraphNode>> GetDownstreamDependentsAsync(int projectId, string rootType, int rootId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for Dependency operations
/// </summary>
public class DependencyRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<DependencyRepository> logger) : BaseRepository(connectionFactory, logger), IDependencyRepository
{
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

            var deleteSql = DependencyQueries.DeleteDependencies;

            var deleteParams = new
            {
                ProjectId = projectId,
                SourceIds = sourceIds,
                SourceTypes = sourceTypes
            };

            await conn.ExecuteAsync(deleteSql, deleteParams, transaction);

            // 2. Insert new dependencies
            var insertSql = DependencyQueries.InsertDependency;

            await conn.ExecuteAsync(insertSql, depsList, transaction);

            _logger.LogInformation(
                "Saved {Count} dependencies for project {ProjectId} (sources: {SourceCount})",
                depsList.Count,
                projectId,
                sourceIds.Count);

            return true; // Return value required by ExecuteInTransactionAsync
        }, cancellationToken);
    }

    public async Task<List<ResolvedDependency>> GetDependentsAsync(int projectId, string targetType, int targetId, CancellationToken cancellationToken = default)
    {
        var sql = DependencyQueries.GetDependents;

        var dependencies = await QueryAsync<ResolvedDependency>(sql, new 
        { 
            ProjectId = projectId, 
            TargetType = targetType, 
            TargetId = targetId 
        }, cancellationToken);

        return dependencies.ToList();
    }

    public async Task<List<DependencyGraphNode>> GetDownstreamDependentsAsync(int projectId, string rootType, int rootId, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        
        // This Recursive CTE finds ALL downstream dependencies, not just direct ones.
        // It tracks "Depth" to show how far away the impact is.
        var sql = DependencyQueries.GetDownstreamDependents;

        return (await conn.QueryAsync<DependencyGraphNode>(sql, new { ProjectId = projectId, RootType = rootType, RootId = rootId })).ToList();
    }
        public async Task ClearDependenciesAsync(int projectId, string entityType, int entityId, IDbConnection conn, IDbTransaction transaction)
    {
        var sql = DependencyQueries.ClearDependencies;
        await conn.ExecuteAsync(sql, new { ProjectId = projectId, EntityType = entityType, EntityId = entityId }, transaction);
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


public class DependencyGraphNode : Dependency
{
    public int Depth { get; set; }
    public string? Path { get; set; }
    public string? EntityName { get; set; }
    public string? DataOwner { get; set; }
    public int CriticalityLevel { get; set; } // 1-5
}