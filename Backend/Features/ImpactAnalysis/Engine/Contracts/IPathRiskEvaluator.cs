using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;

public interface IPathRiskEvaluator
{
    string Version { get; }

    IReadOnlyDictionary<string, object> PolicySnapshot { get; }

    DependencyPath Evaluate(
        DependencyPath path,
        ChangeType changeType);
}