using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;
public interface IGraphBuilder
{
    ImpactGraph Build(IReadOnlyList<DependencyGraphRow> rows);
}