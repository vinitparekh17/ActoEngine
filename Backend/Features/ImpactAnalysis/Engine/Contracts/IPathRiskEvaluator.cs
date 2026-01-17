using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

public interface IPathRiskEvaluator
{
    string Version { get; }

    IReadOnlyDictionary<string, object> PolicySnapshot { get; }

    DependencyPath Evaluate(
        DependencyPath path,
        ChangeType changeType);
}