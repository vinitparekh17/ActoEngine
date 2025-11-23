using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Database;
using Dapper;
using System.Data;

namespace ActoEngine.WebApi.Services.Intelligence;

public interface IDependencyResolutionService
{
    /// <summary>
    /// Resolves entity names to IDs and persists them to the Dependencies table.
    /// </summary>
    Task ResolveAndSaveDependenciesAsync(int projectId, List<Dependency> rawDependencies);
}

public class DependencyResolutionService(
    IDbConnectionFactory connectionFactory,
    IDependencyRepository dependencyRepository,
    ILogger<DependencyResolutionService> logger) : IDependencyResolutionService
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;
    private readonly IDependencyRepository _dependencyRepository = dependencyRepository;
    private readonly ILogger<DependencyResolutionService> _logger = logger;

    public async Task ResolveAndSaveDependenciesAsync(int projectId, List<Dependency> rawDependencies)
    {
        if (rawDependencies.Count == 0) return;

        using var conn = await _connectionFactory.CreateConnectionAsync();

        // 1. Pre-load map of Table Names -> IDs for this project
        var tableMap = await LoadTableMapAsync(conn, projectId);

        // 2. Pre-load map of SP Names -> IDs for this project
        var spMap = await LoadSpMapAsync(conn, projectId);

        var validDependencies = new List<ResolvedDependency>();

        foreach (var dep in rawDependencies)
        {
            int? targetId = null;
            string cleanName = CleanName(dep.TargetName);

            // Try exact match first
            if (dep.TargetType == "TABLE")
            {
                targetId = FindEntityId(tableMap, cleanName);
            }
            else if (dep.TargetType == "SP")
            {
                targetId = FindEntityId(spMap, cleanName);
            }

            if (targetId.HasValue)
            {
                validDependencies.Add(new ResolvedDependency
                {
                    ProjectId = projectId,
                    SourceType = dep.SourceType,
                    SourceId = dep.SourceId,
                    TargetType = dep.TargetType,
                    TargetId = targetId.Value,
                    DependencyType = dep.DependencyType,
                    ConfidenceScore = 1.0m // Parsed from code = high confidence
                });
            }
            else
            {
                // Log missed dependencies? (Optional: Helps debug parser issues)
                // _logger.LogDebug("Could not resolve dependency {Name} in project {Id}", dep.TargetName, projectId);
            }
        }

        // 3. Save to database using repository (handles transaction internally)
        if (validDependencies.Count != 0)
        {
            await _dependencyRepository.SaveDependenciesForSourcesAsync(projectId, validDependencies);
        }
    }

    private static int? FindEntityId(Dictionary<string, int> map, string name)
    {
        // 1. Try exact match (Schema.Table)
        if (map.TryGetValue(name, out var id)) return id;

        // 2. Try without schema (if code says "Users" but DB has "dbo.Users")
        if (!name.Contains('.'))
        {
            // Check if any key ends with ".Name"
            var match = map.Keys.FirstOrDefault(k => k.EndsWith($".{name}", StringComparison.OrdinalIgnoreCase));
            if (match != null) return map[match];
            
            // Check for default dbo
            if (map.TryGetValue($"dbo.{name}", out var dboId)) return dboId;
        }

        return null;
    }

    private static string CleanName(string name)
    {
        // First, strip all bracket characters (not just start/end)
        string cleaned = name.Replace("[", "").Replace("]", "");

        // Then normalize schema qualifier (remove "dbo." if present)
        const string dboPrefix = "dbo.";
        if (cleaned.StartsWith(dboPrefix, StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring(dboPrefix.Length);
        }

        return cleaned;
    }

    private async Task<Dictionary<string, int>> LoadTableMapAsync(IDbConnection conn, int projectId)
    {
        var sql = @"SELECT TableName, SchemaName, TableId FROM TablesMetadata WHERE ProjectId = @ProjectId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { ProjectId = projectId });

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unqualifiedTracker = new Dictionary<string, List<(string Schema, int TableId)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            string tableName = row.TableName;
            string schema = row.SchemaName;
            string fullName = $"{schema}.{tableName}";
            int tableId = row.TableId;

            // Always add qualified name
            map[fullName] = tableId;

            // Track all occurrences for unqualified name
            if (!unqualifiedTracker.ContainsKey(tableName))
                unqualifiedTracker[tableName] = new List<(string, int)>();
            unqualifiedTracker[tableName].Add((schema, tableId));
        }

        // Resolve unqualified names with preference and warnings
        foreach (var (tableName, occurrences) in unqualifiedTracker)
        {
            if (occurrences.Count == 1)
            {
                // Unique - safe to map
                map[tableName] = occurrences[0].TableId;
            }
            else
            {
                // Collision - prefer dbo
                var dboEntry = occurrences.FirstOrDefault(e => e.Schema.Equals("dbo", StringComparison.OrdinalIgnoreCase));

                if (dboEntry != default)
                {
                    map[tableName] = dboEntry.TableId;
                    _logger.LogWarning(
                        "Table name '{TableName}' exists in multiple schemas: {Schemas}. Using dbo.{TableName} (Id: {TableId}) for unqualified references.",
                        tableName,
                        string.Join(", ", occurrences.Select(e => e.Schema)),
                        tableName,
                        dboEntry.TableId);
                }
                else
                {
                    // No dbo - use first but warn
                    map[tableName] = occurrences[0].TableId;
                    _logger.LogWarning(
                        "Table name '{TableName}' exists in multiple schemas: {Schemas}. Using {Schema}.{TableName} (Id: {TableId}) for unqualified references. Consider using qualified names.",
                        tableName,
                        string.Join(", ", occurrences.Select(e => e.Schema)),
                        occurrences[0].Schema,
                        tableName,
                        occurrences[0].TableId);
                }
            }
        }

        return map;
    }

    private async Task<Dictionary<string, int>> LoadSpMapAsync(IDbConnection conn, int projectId)
    {
        var sql = @"SELECT ProcedureName, SchemaName, SpId FROM SpMetadata WHERE ProjectId = @ProjectId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { ProjectId = projectId });

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unqualifiedTracker = new Dictionary<string, List<(string Schema, int SpId)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            string name = row.ProcedureName;
            string? schema = row.SchemaName;
            
            // Normalize null/empty schema to "dbo" to prevent incorrect mappings
            if (string.IsNullOrEmpty(schema))
            {
                schema = "dbo";
                _logger.LogDebug(
                    "Stored procedure '{SpName}' has null/empty schema, normalized to 'dbo'",
                    name);
            }

            // Always add qualified name with normalized schema
            string fullName = $"{schema}.{name}";
            map[fullName] = row.SpId;
            
            // Track all occurrences for unqualified name resolution
            if (!unqualifiedTracker.ContainsKey(name))
                unqualifiedTracker[name] = new List<(string, int)>();
            unqualifiedTracker[name].Add((schema, row.SpId));
        }

        // Resolve unqualified names with preference and warnings
        foreach (var (spName, occurrences) in unqualifiedTracker)
        {
            if (occurrences.Count == 1)
            {
                // Unique - safe to map
                map[spName] = occurrences[0].SpId;
            }
            else
            {
                // Collision - prefer dbo
                var dboEntry = occurrences.FirstOrDefault(e => e.Schema.Equals("dbo", StringComparison.OrdinalIgnoreCase));

                if (dboEntry != default)
                {
                    map[spName] = dboEntry.SpId;
                    _logger.LogWarning(
                        "Stored procedure name '{SpName}' exists in multiple schemas: {Schemas}. Using dbo.{SpName} (Id: {SpId}) for unqualified references.",
                        spName,
                        string.Join(", ", occurrences.Where(e => !string.IsNullOrEmpty(e.Schema)).Select(e => e.Schema)),
                        spName,
                        dboEntry.SpId);
                }
                else
                {
                    // No dbo - use first but warn
                    map[spName] = occurrences[0].SpId;
                    var schemas = occurrences.Where(e => !string.IsNullOrEmpty(e.Schema)).Select(e => e.Schema).ToList();
                    if (schemas.Count > 0)
                    {
                        _logger.LogWarning(
                            "Stored procedure name '{SpName}' exists in multiple schemas: {Schemas}. Using {Schema}.{SpName} (Id: {SpId}) for unqualified references. Consider using qualified names.",
                            spName,
                            string.Join(", ", schemas),
                            occurrences[0].Schema,
                            spName,
                            occurrences[0].SpId);
                    }
                }
            }
        }

        return map;
    }

}