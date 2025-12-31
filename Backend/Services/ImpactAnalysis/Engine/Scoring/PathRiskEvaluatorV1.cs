using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;
using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Scoring;


/// <summary>
/// Version 1.0 of path risk evaluation.
/// Deterministic, depth-aware, worst-case driven.
/// </summary>
public sealed class PathRiskEvaluatorV1 : IPathRiskEvaluator
{
    public string Version => "v1.0";

    /// <summary>
    /// Serializable snapshot of scoring policy used for this evaluator.
    /// This must be embedded into ImpactResult for audit replay.
    /// </summary>
    public object PolicySnapshot => new
    {
        Version = Version,
        DepthDecayFactor = 0.2,
        DependencyWeights = new Dictionary<DependencyType, int>
        {
            { DependencyType.Delete, 10 },
            { DependencyType.Update, 8 },
            { DependencyType.Insert, 7 },
            { DependencyType.Select, 4 },
            { DependencyType.SchemaDependency, 9 },
            { DependencyType.ApiCall, 6 },
            { DependencyType.Unknown, 5 }
        },
        ChangeTypeMultipliers = new Dictionary<ChangeType, int>
        {
            { ChangeType.Delete, 3 },
            { ChangeType.Modify, 2 },
            { ChangeType.Create, 1 }
        },
        CriticalityScale = "1-5"
    };

    public DependencyPath Evaluate(
        DependencyPath path,
        ChangeType changeType)
    {
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
        return type switch
        {
            DependencyType.Delete => 10,
            DependencyType.Update => 8,
            DependencyType.Insert => 7,
            DependencyType.SchemaDependency => 9,
            DependencyType.ApiCall => 6,
            DependencyType.Select => 4,
            DependencyType.Unknown => 5,
            _ => 5
        };
    }

    private static int GetChangeMultiplier(ChangeType changeType)
    {
        return changeType switch
        {
            ChangeType.Delete => 3,
            ChangeType.Modify => 2,
            ChangeType.Create => 1,
            _ => 1
        };
    }

    private static double CalculateDepthFactor(int depth)
    {
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