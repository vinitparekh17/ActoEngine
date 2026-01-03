using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;

public interface IImpactFacade
{
    Task<ImpactResult> AnalyzeAsync(
        int projectId,
        EntityRef rootEntity,
        ChangeType changeType,
        CancellationToken cancellationToken = default);
}