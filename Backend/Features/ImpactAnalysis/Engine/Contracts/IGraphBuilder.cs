using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

public interface IGraphBuilder
{
    ImpactGraph Build(IReadOnlyList<DependencyGraphRow> rows);
}