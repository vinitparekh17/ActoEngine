namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

/// <summary>
/// Represents a single dependency edge discovered from the database.
/// This is a raw traversal DTO, not a domain object.
/// </summary>
public sealed class DependencyGraphRow
{
    // Dependent (the thing that is impacted)
    public required string SourceEntityType { get; init; }
    public required int SourceEntityId { get; init; }

    // Dependency (the thing it depends on)
    public required string TargetEntityType { get; init; }
    public required int TargetEntityId { get; init; }

    public required string DependencyType { get; init; }

    // Traversal metadata
    public required int Depth { get; init; }

    // Optional enrichment (used later, never for traversal)
    public string? SourceEntityName { get; init; }
    public string? TargetEntityName { get; init; }
    public int? SourceCriticalityLevel { get; init; }
}