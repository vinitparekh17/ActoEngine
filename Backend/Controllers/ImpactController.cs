using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Services.ImpactAnalysis.Mapping;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.VerdictBuilder;

namespace ActoEngine.WebApi.Controllers;

/// <summary>
/// Impact analysis API.
/// Thin controller using standard ApiResponse envelope.
/// </summary>
[ApiController]
[Route("api/impact")]
public sealed class ImpactController(
    IImpactFacade impactFacade,
    ImpactVerdictBuilder verdictBuilder) : ControllerBase
{

    [HttpGet("~/api/projects/{projectId:int}/impact/{entityType}/{entityId:int}")]
    public async Task<ActionResult<ApiResponse<ImpactDecisionResponse>>> Analyze(
        [FromRoute] int projectId,
        [FromRoute] string entityType,
        [FromRoute] int entityId,
        [FromQuery] string changeType,
        CancellationToken cancellationToken)
    {
        // -----------------------------
        // Input validation
        // -----------------------------

        if (!Enum.TryParse<EntityType>(
                entityType,
                ignoreCase: true,
                out var parsedEntityType))
        {
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

        var analysis = await impactFacade.AnalyzeAsync(
            projectId,
            rootEntity,
            parsedChangeType,
            cancellationToken);

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

            // Aggregated summary (existing engine output)
            Summary = analysis.OverallImpact,

            // Ranked impacts (trimmed, not exhaustive)
            Entities = analysis.EntityImpacts
                .OrderByDescending(e => e.WorstCaseImpactLevel)
                .ThenByDescending(e => e.WorstCaseRiskScore)
                .Take(10)
                .Cast<object>()
                .ToList(),

            // Evidence (UI can hide)
            Paths = analysis.Paths,
            Graph = null // TODO (graphs not yet supported)
        };

        return Ok(
            ApiResponse<ImpactDecisionResponse>.Success(
                response,
                "Impact analysis completed"
            ));
    }
}