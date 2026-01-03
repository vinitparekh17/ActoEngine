using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;
using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Graph;


/// <summary>
/// Builds an immutable ImpactGraph from raw dependency rows.
/// Pure normalization layer: no traversal, no scoring.
/// </summary>
public sealed class GraphBuilder : IGraphBuilder
{
    public ImpactGraph Build(IReadOnlyList<DependencyGraphRow> rows)
    {
        var nodes = new Dictionary<EntityRef, GraphNode>();
        var adjacency = new Dictionary<EntityRef, List<GraphEdge>>();

        foreach (var row in rows)
        {
            // ---- Build EntityRefs ----

            var sourceType = ParseEntityType(row.SourceEntityType);
            var targetType = ParseEntityType(row.TargetEntityType);

            var sourceEntity = new EntityRef(
                sourceType,
                row.SourceEntityId,
                row.SourceEntityName);

            var targetEntity = new EntityRef(
                targetType,
                row.TargetEntityId,
                row.TargetEntityName);

            // ---- Register Nodes ----
            // Source entities: use explicit criticality when available
            var sourceCriticality = NormalizeCriticality(row.SourceCriticalityLevel);
            if (!nodes.TryGetValue(sourceEntity, out var existingSourceNode))
            {
                nodes[sourceEntity] = new GraphNode
                {
                    Entity = sourceEntity,
                    CriticalityLevel = sourceCriticality
                };
            }
            else if (row.SourceCriticalityLevel.HasValue && existingSourceNode.CriticalityLevel == 3)
            {
                // Only overwrite when existing node has default criticality (3)
                // This prevents order-dependent behavior when multiple rows reference the same entity
                nodes[sourceEntity] = new GraphNode
                {
                    Entity = sourceEntity,
                    CriticalityLevel = sourceCriticality
                };
            }

            // Target entities: only set default if not already known
            if (!nodes.ContainsKey(targetEntity))
            {
                nodes[targetEntity] = new GraphNode
                {
                    Entity = targetEntity,
                    CriticalityLevel = 3 // default medium
                };
            }

            // ---- Build Edge (dependency → dependent) ----

            var dependencyType = ParseDependencyType(row.DependencyType);

            var edge = new GraphEdge
            {
                From = targetEntity, // dependency
                To = sourceEntity,   // dependent
                DependencyType = dependencyType
            };

            if (!adjacency.TryGetValue(targetEntity, out var edges))
            {
                edges = [];
                adjacency[targetEntity] = edges;
            }

            edges.Add(edge);
        }

        return new ImpactGraph(nodes, adjacency);
    }

    // -----------------------------
    // Parsing & Normalization
    // -----------------------------

    private static EntityType ParseEntityType(string raw)
    {
        if (Enum.TryParse<EntityType>(raw, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Unknown EntityType '{raw}'. Ensure enum is updated.");
    }

    private static DependencyType ParseDependencyType(string raw)
    {
        if (Enum.TryParse<DependencyType>(raw, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return DependencyType.Unknown;
    }

    private static int NormalizeCriticality(int? raw)
    {
        if (!raw.HasValue)
        {
            return 3;
        }

        if (raw.Value < 1)
        {
            return 1;
        }

        if (raw.Value > 5)
        {
            return 5;
        }

        return raw.Value;
    }
}