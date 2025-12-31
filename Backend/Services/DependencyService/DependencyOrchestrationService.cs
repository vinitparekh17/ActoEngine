using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.DependencyService;

public interface IDependencyOrchestrationService
{
    /// <summary>
    /// Analyzes all stored procedures in the project to find and resolve dependencies.
    /// Run this AFTER a schema sync is committed.
    /// </summary>
    Task AnalyzeProjectAsync(int projectId);
}

public class DependencyOrchestrationService(
    ISchemaRepository schemaRepository,
    IDependencyAnalysisService analysisService,
    IDependencyResolutionService resolutionService,
    ILogger<DependencyOrchestrationService> logger) : IDependencyOrchestrationService
{
    private readonly ISchemaRepository _schemaRepository = schemaRepository;
    private readonly IDependencyAnalysisService _analysisService = analysisService;
    private readonly IDependencyResolutionService _resolutionService = resolutionService;
    private readonly ILogger<DependencyOrchestrationService> _logger = logger;

    public async Task AnalyzeProjectAsync(int projectId)
    {
        _logger.LogInformation("Starting dependency analysis for project {ProjectId}", projectId);

        try
        {
            // 1. Fetch all SPs (definitions are needed)
            var procedures = await _schemaRepository.GetStoredStoredProceduresAsync(projectId);
            _logger.LogInformation("Fetched {Count} stored procedures for analysis", procedures.Count);

            var allRawDeps = new List<Dependency>();
            var failureCount = 0;

            // 2. Sequential Extraction (Safety First)
            foreach (var sp in procedures)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(sp.Definition))
                    {
                        continue;
                    }

                    // Extract raw dependencies from SQL definition
                    var deps = _analysisService.ExtractDependencies(sp.Definition, sp.SpId, "SP");
                    allRawDeps.AddRange(deps);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    // Log but continue - partial failure is acceptable
                    _logger.LogWarning(ex, "Failed to analyze dependencies for SP {SpId} ({SpName})", sp.SpId, sp.ProcedureName);
                }
            }

            // 3. Deduplicate
            // Distinct based on content to ensure idempotency
            var uniqueRawDeps = allRawDeps
                .GroupBy(d => new { d.SourceType, d.SourceId, d.TargetType, d.TargetName, d.DependencyType })
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation(
                "Extracted {TotalRaw} raw dependencies (Unique: {UniqueRaw}). Failures: {Failures}",
                allRawDeps.Count,
                uniqueRawDeps.Count,
                failureCount);

            // 4. Batch Resolution & Save
            if (uniqueRawDeps.Count > 0)
            {
                await _resolutionService.ResolveAndSaveDependenciesAsync(projectId, uniqueRawDeps);
            }

            _logger.LogInformation("Dependency analysis completed successfully for project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            // If the whole process fails (e.g. database down), log it.
            // We do NOT rethrow because we don't want to crash the calling background job if possible,
            // or at least we want to control how it fails.
            _logger.LogError(ex, "Critical failure during dependency analysis for project {ProjectId}", projectId);
            throw; // Rethrowing here so the caller (ProjectService) knows it failed, though ProjectService will likely just log it too.
        }
    }
}
