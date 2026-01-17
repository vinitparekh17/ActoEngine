namespace ActoEngine.WebApi.Features.ImpactAnalysis;

public class Dependency
{
    public int SourceId { get; set; }
    public required string SourceType { get; set; }
    public int TargetId { get; set; }
    public required string TargetName { get; set; }
    public required string TargetType { get; set; }
    public required string DependencyType { get; set; }
}
