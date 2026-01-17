using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

public interface IImpactFacade
{
    Task<ImpactResult> AnalyzeAsync(
        int projectId,
        EntityRef rootEntity,
        ChangeType changeType,
        CancellationToken cancellationToken = default);
}