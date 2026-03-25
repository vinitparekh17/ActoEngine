using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.VerdictBuilder;
using ActoEngine.WebApi.Features.LogicalFk;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Features.ImpactAnalysis;

/// <summary>
/// Impact analysis API.
/// Thin controller using standard ApiResponse envelope.
/// </summary>
[ApiController]
[Route("api/impact")]
public sealed class ImpactController(
    IImpactFacade impactFacade,
    ImpactVerdictBuilder verdictBuilder,
    IDependencyOrchestrationService dependencyOrchestrationService,
    ILogicalFkService logicalFkService,
    ILogger<ImpactController> logger) : ControllerBase
{
    private readonly ILogger<ImpactController> _logger = logger;

    [HttpGet("~/api/projects/{projectId:int}/impact/{entityType}/{entityId:int}")]
    public async Task<ActionResult<ApiResponse<ImpactDecisionResponse>>> Analyze(
        [FromRoute] int projectId,
        [FromRoute] string entityType,
        [FromRoute] int entityId,
        [FromQuery] string changeType = "MODIFY", // Default to MODIFY for backward compatibility
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Impact analysis requested: ProjectId={ProjectId}, EntityType={EntityType}, EntityId={EntityId}, ChangeType={ChangeType}",
            projectId, entityType, entityId, changeType);

        // -----------------------------
        // Input validation
        // -----------------------------

        if (!Enum.TryParse<EntityType>(
                entityType,
                ignoreCase: true,
                out var parsedEntityType))
        {
            _logger.LogWarning("Invalid entityType '{EntityType}' provided", entityType);
            return BadRequest(
                ApiResponse<ImpactDecisionResponse>.Failure(
                    "Invalid entityType",
                    [$"Unsupported entityType '{entityType}'"]
                ));
        }

        if (!Enum.TryParse<ChangeType>(
                changeType,
                ignoreCase: true,
                out var parsedChangeType))
        {
            _logger.LogWarning("Invalid changeType '{ChangeType}' provided", changeType);
            return BadRequest(
                ApiResponse<ImpactDecisionResponse>.Failure(
                    "Invalid changeType",
                    [$"Unsupported changeType '{changeType}'"]
                ));
        }

        var rootEntity = new EntityRef(parsedEntityType, entityId);

        // -----------------------------
        // 1. Run factual analysis (engine)
        // -----------------------------

        _logger.LogDebug("Starting impact analysis for {RootEntity}", rootEntity.StableKey);

        var analysis = await impactFacade.AnalyzeAsync(
            projectId,
            rootEntity,
            parsedChangeType,
            cancellationToken);

        _logger.LogDebug("Impact analysis completed with {PathCount} paths", analysis.Paths.Count);

        // -----------------------------
        // 2. Build verdict (NEW, opinionated)
        // -----------------------------

        var verdict = verdictBuilder.Build(analysis);

        // -----------------------------
        // 3. Shape verdict-first response
        // -----------------------------

        var response = new ImpactDecisionResponse
        {
            Verdict = verdict,

            // Aggregated summary (existing engine output) + Frontend context
            Summary = new
            {
                analysis.OverallImpact.WorstImpactLevel,
                analysis.OverallImpact.WorstRiskScore,
                analysis.OverallImpact.TriggeringEntity,
                analysis.OverallImpact.TriggeringPathId,
                analysis.OverallImpact.RequiresApproval,

                // Frontend specific context
                RootEntity = analysis.RootEntity,
                Environment = "Production", // Placeholder
                AnalysisType = "Impact Analysis",
                Action = changeType ?? "Modify"
            },

            // Ranked impacts (trimmed, not exhaustive)
            Entities = [.. analysis.EntityImpacts
                .OrderByDescending(e => e.WorstCaseImpactLevel)
                .ThenByDescending(e => e.WorstCaseRiskScore)
                .Take(10)
                .Cast<object>()],

            // Evidence (UI can hide)
            Paths = analysis.Paths,
            Graph = null // TODO (graphs not yet supported)
        };

        _logger.LogInformation(
            "Impact analysis completed successfully for ProjectId={ProjectId}, EntityType={EntityType}, EntityId={EntityId}",
            projectId, entityType, entityId);

        return Ok(
            ApiResponse<ImpactDecisionResponse>.Success(
                response,
                "Impact analysis completed"
            ));
    }

    /// <summary>
    /// Re-runs dependency extraction from all SPs + logical FK detection.
    /// Call this after making changes to SP definitions or schema.
    /// </summary>
    [HttpPost("~/api/projects/{projectId:int}/reanalyze")]
    public async Task<ActionResult<ApiResponse<ReanalyzeResponse>>> Reanalyze(
        [FromRoute] int projectId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Re-analyze requested for ProjectId={ProjectId}", projectId);

        try
        {
            // 1. Re-extract SP dependencies (delete + rebuild)
            await dependencyOrchestrationService.AnalyzeProjectAsync(projectId);

            // 2. Re-run logical FK detection
            await logicalFkService.DetectAndPersistCandidatesAsync(projectId, cancellationToken);

            _logger.LogInformation(
                "Re-analyze completed for ProjectId={ProjectId}", projectId);

            return Ok(ApiResponse<ReanalyzeResponse>.Success(
                new ReanalyzeResponse
                {
                    Success = true,
                    Message = "Dependencies and logical FK detection re-analyzed successfully."
                },
                "Re-analysis completed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Re-analyze failed for ProjectId={ProjectId}", projectId);

            return StatusCode(500, ApiResponse<ReanalyzeResponse>.Failure(
                "Re-analysis failed",
                [ex.Message]));
        }
    }
}