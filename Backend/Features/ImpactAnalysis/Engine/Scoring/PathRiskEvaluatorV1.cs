using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Scoring;


/// <summary>
/// Version 1.0 of path risk evaluation.
/// Deterministic, depth-aware, worst-case driven.
/// </summary>
public sealed class PathRiskEvaluatorV1 : IPathRiskEvaluator
{
    public string Version => "v1.0";

    /// <summary>
    /// Canonical source for dependency type weights.
    /// Used by both PolicySnapshot and GetDependencyWeight.
    /// </summary>
    private static readonly IReadOnlyDictionary<DependencyType, int> DependencyWeightsSource = new Dictionary<DependencyType, int>
    {
        { DependencyType.Delete, 10 },
        { DependencyType.Update, 8 },
        { DependencyType.Insert, 7 },
        { DependencyType.Select, 4 },
        { DependencyType.SchemaDependency, 9 },
        { DependencyType.ApiCall, 6 },
        { DependencyType.LogicalFk, 6 },
        { DependencyType.Unknown, 5 }
    };

    /// <summary>
    /// Canonical source for change type multipliers.
    /// Used by both PolicySnapshot and GetChangeMultiplier.
    /// </summary>
    private static readonly IReadOnlyDictionary<ChangeType, int> ChangeTypeMultipliersSource = new Dictionary<ChangeType, int>
    {
        { ChangeType.Delete, 3 },
        { ChangeType.Modify, 2 },
        { ChangeType.Create, 1 }
    };

    private const double DepthDecayFactor = 0.2;
    private const int DefaultWeight = 5;
    private const int DefaultMultiplier = 1;

    /// <summary>
    /// Cached, immutable snapshot — avoids allocating a new Dictionary on every access.
    /// Built once from canonical sources at class-load time.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, object> CachedPolicySnapshot =
        new Dictionary<string, object>
        {
            ["Version"] = "v1.0",
            ["DepthDecayFactor"] = DepthDecayFactor,
            ["DependencyWeights"] = DependencyWeightsSource.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value),
            ["ChangeTypeMultipliers"] = ChangeTypeMultipliersSource.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value),
            ["CriticalityScale"] = "1-5"
        };

    /// <summary>
    /// Serializable snapshot of scoring policy used for this evaluator.
    /// This must be embedded into ImpactResult for audit replay.
    /// </summary>
    public IReadOnlyDictionary<string, object> PolicySnapshot => CachedPolicySnapshot;

    public DependencyPath Evaluate(
        DependencyPath path,
        ChangeType changeType)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Nodes == null || path.Nodes.Count == 0)
        {
            throw new ArgumentException("Path must have at least one node.", nameof(path));
        }

        // 1. Base dependency weight (worst interaction dominates)
        int dependencyWeight = GetDependencyWeight(path.MaxDependencyType);

        // 2. Change-type multiplier
        int changeMultiplier = GetChangeMultiplier(changeType);

        // 3. Criticality factor (already bounded 1–5)
        int criticalityFactor = path.MaxCriticalityLevel;

        // 4. Depth decay (domain rule)
        double depthFactor = CalculateDepthFactor(path.Depth);

        // 5. Final raw score
        double rawScore =
            dependencyWeight *
            changeMultiplier *
            criticalityFactor *
            depthFactor;

        int finalScore = (int)Math.Round(rawScore);

        // 6. Impact level classification
        ImpactLevel impactLevel = ClassifyImpact(finalScore);

        // 7. Return NEW scored path (immutability preserved)
        return new DependencyPath
        {
            PathId = path.PathId,
            Nodes = path.Nodes,
            Edges = path.Edges,
            Depth = path.Depth,
            MaxDependencyType = path.MaxDependencyType,
            MaxCriticalityLevel = path.MaxCriticalityLevel,

            RiskScore = finalScore,
            ImpactLevel = impactLevel,

            DominantEntity = path.Nodes[^1],
            DominantDependencyType = path.MaxDependencyType
        };
    }

    private static int GetDependencyWeight(DependencyType type)
    {
        return DependencyWeightsSource.TryGetValue(type, out var weight) ? weight : DefaultWeight;
    }

    private static int GetChangeMultiplier(ChangeType changeType)
    {
        return ChangeTypeMultipliersSource.TryGetValue(changeType, out var multiplier) ? multiplier : DefaultMultiplier;
    }

    private static double CalculateDepthFactor(int depth)
    {
        if (depth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth must be >= 1.");
        }

        // Depth 1 = 1.0
        // Depth 2 = 0.8
        // Depth 3 = 0.6
        // Minimum floor = 0.2
        double factor = 1.0 - ((depth - 1) * 0.2);
        return Math.Max(0.2, factor);
    }

    private static ImpactLevel ClassifyImpact(int score)
    {
        if (score >= 50)
        {
            return ImpactLevel.Critical;
        }

        if (score >= 30)
        {
            return ImpactLevel.High;
        }

        if (score >= 15)
        {
            return ImpactLevel.Medium;
        }

        if (score > 0)
        {
            return ImpactLevel.Low;
        }

        return ImpactLevel.None;
    }
}
