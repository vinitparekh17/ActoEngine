using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.VerdictBuilder;

public sealed class ImpactVerdictBuilder
{
    public ImpactVerdict Build(ImpactResult analysis)
    {
        var risk = MapRisk(analysis.OverallImpact.WorstImpactLevel);
        var reasons = BuildReasons(analysis);

        return new ImpactVerdict
        {
            Risk = risk,
            RequiresApproval = analysis.OverallImpact.RequiresApproval,
            Summary = BuildSummary(risk, reasons),
            Reasons = reasons,
            Limitations = DetectLimitations(analysis),
            GeneratedAt = DateTime.UtcNow
        };
    }

    // -----------------------------
    // Risk mapping (unchanged)
    // -----------------------------

    private static RiskLevel MapRisk(ImpactLevel level) => level switch
    {
        ImpactLevel.Critical => RiskLevel.Critical,
        ImpactLevel.High => RiskLevel.High,
        ImpactLevel.Medium => RiskLevel.Medium,
        ImpactLevel.Low => RiskLevel.Low,
        _ => RiskLevel.Unknown
    };

    // -----------------------------
    // Reasons (improved language only)
    // -----------------------------

    private static List<VerdictReason> BuildReasons(ImpactResult analysis)
    {
        var rootEntityType = analysis.RootEntity.Type;
        var reasons = new List<VerdictReason>();
        var rootEntityNoun = FormatEntityType(rootEntityType);

        // var rankedEntities = analysis.EntityImpacts
        //     .OrderByDescending(e => e.WorstCaseImpactLevel)
        //     .ThenByDescending(e => e.Paths.Count)
        //     .ToList();
        var rankedEntities = analysis.EntityImpacts
            .SelectMany(e => e.Paths
                .Where(p => p.Depth == 1)
                .Select(p => new
                {
                    e.Entity,
                    EntityType = e.Entity.Type,
                    DependencyType = p.DominantDependencyType
                }))
            .GroupBy(x => new { x.EntityType, x.DependencyType })
            .Select(g => new
            {
                g.Key.EntityType,
                g.Key.DependencyType,
                Count = g.Count(),
                Entities = g.Select(x => x.Entity).Distinct().ToList()
            })
            .OrderByDescending(g => g.Count)
            .ToList();



        if (rankedEntities.Count == 0)
        {
            reasons.Add(new VerdictReason
            {
                Priority = 1,
                Statement = "No dependent entities detected",
                Implication = "Either the entity is unused or dependency metadata is incomplete",
                Evidence = []
            });
            return reasons;
        }

        // Primary reason (improved statement)
        var primary = rankedEntities.First();

        reasons.Add(new VerdictReason
        {
            Priority = 1,
            Statement = BuildGroupedStatement(primary, rootEntityNoun),
            Implication = BuildGroupedImplication(primary),
            Evidence = [.. primary.Entities.Select(e => e.StableKey)]
        });

        if (analysis.Paths.All(p => p.Edges.All(e => e == DependencyType.Select)))
        {
            reasons.Add(new VerdictReason
            {
                Priority = 3,
                Statement = "All detected dependencies are read-only",
                Implication = "Lower risk of data corruption",
                Evidence = []
            });
        }

        return reasons;
    }

    private static string BuildGroupedImplication(dynamic group)
    {
        if (group.DependencyType == DependencyType.Delete ||
            group.DependencyType == DependencyType.Update)
            return "IMPORTANT: Data modification logic will be affected";

        if (group.EntityType == EntityType.Sp)
            return "Coordinate testing across these procedures";

        return "Review dependent components during testing";
    }


    private static string BuildGroupedStatement(dynamic group, string rootEntityNoun)
    {
        var noun = group.EntityType switch
        {
            EntityType.Sp => "stored procedure",
            EntityType.View => "view",
            EntityType.Function => "function",
            _ => "entity"
        };

        var plural = group.Count == 1 ? "" : "s";
        var verb = GetVerb(group.DependencyType);

        return $"{group.Count} {noun}{plural} {verb} this {rootEntityNoun}";
    }


    // -----------------------------
    // ONLY CHANGE: Better statement formatting
    // -----------------------------

    private static VerdictReason BuildEntityReason(
        EntityImpact impact,
        int priority,
        IReadOnlyList<DependencyPath> allPaths)
    {
        int directDeps = impact.Paths.Count(p => p.Depth == 1);

        // Entity name (fallback if null)
        string entityName = impact.Entity.Name ?? $"{impact.Entity.Type}:{impact.Entity.Id}";

        // Format entity type for display
        string entityTypeDisplay = FormatEntityType(impact.Entity.Type);

        // Get dominant operation from this entity's paths
        var dominantOp = GetDominantOperation(allPaths);

        // Build better statement
        // string statement = BuildStatement(directDeps, entityTypeDisplay, dominantOp, entityName);
        string statement = BuildStatement(directDeps, entityTypeDisplay, dominantOp);

        // Get context-aware implication
        string implication = GetImplication(
            impact.Entity.Type,
            dominantOp,
            impact.WorstCaseImpactLevel
        );

        // Extract evidence (unchanged - this is correct)
        // var evidence = impact.Paths
        //     .Select(p => p.Nodes[^1].Name)
        //     .Where(n => !string.IsNullOrWhiteSpace(n))
        //     .Distinct()
        //     .Take(5)
        //     .ToList();
        var evidence = impact.Paths
            .Select(p => p.Nodes[^1].StableKey)
            .Distinct()
            .Take(5)
            .ToList();


        return new VerdictReason
        {
            Priority = priority,
            Statement = statement,
            Implication = implication,
            Evidence = evidence
        };
    }

    // -----------------------------
    // NEW: Helper methods for better language
    // -----------------------------

    private static string FormatEntityType(EntityType type)
    {
        return type switch
        {
            EntityType.Table => "table",
            EntityType.View => "view",
            EntityType.Sp => "stored procedure",
            EntityType.Function => "function",
            //EntityType.Trigger => "trigger",
            _ => type.ToString().ToLower()
        };
    }

    private static DependencyType GetDominantOperation(IReadOnlyList<DependencyPath> paths)
    {
        // Get all operations, prioritize by severity
        var operations = paths
            .SelectMany(p => p.Edges)
            .GroupBy(e => e)
            .Select(g => new { Op = g.Key, Count = g.Count() })
            .OrderByDescending(x => GetOperationSeverity(x.Op))
            .ThenByDescending(x => x.Count)
            .FirstOrDefault();

        return operations?.Op ?? DependencyType.Select;
    }

    private static int GetOperationSeverity(DependencyType op) => op switch
    {
        DependencyType.Delete => 3,
        DependencyType.Update => 2,
        DependencyType.Insert => 1,
        DependencyType.Select => 0,
        _ => 0
    };

    private static string BuildStatement(
    int count,
    string entityType,
    DependencyType operation,
    string rootEntityNoun = "table")
    {
        string plural = count == 1 ? "" : "s";
        string verb = GetVerb(operation);

        // Root entity is implicit — NEVER show dependent entity identifiers
        return $"{count} {entityType}{plural} {verb} this {rootEntityNoun}";
    }


    private static string GetVerb(DependencyType operation) => operation switch
    {
        DependencyType.Select => "read from",
        DependencyType.Insert => "insert into",
        DependencyType.Update => "update",
        DependencyType.Delete => "delete from",
        _ => "depend on"
    };

    private static string GetImplication(
        EntityType type,
        DependencyType operation,
        ImpactLevel impactLevel)
    {
        // Critical patterns override everything
        //if (type == EntityType.Trigger)
        //    return "CRITICAL: Trigger behavior will change automatically";

        // High severity operations
        if (operation == DependencyType.Delete || operation == DependencyType.Update)
        {
            return type switch
            {
                EntityType.Sp => "IMPORTANT: Data modification logic will be affected",
                EntityType.Table => "Related data modifications will be affected",
                _ => "Write operations will be affected"
            };
        }

        // Normal operations (reads)
        if (impactLevel >= ImpactLevel.High)
            return "Review and test dependent components carefully";

        return type switch
        {
            EntityType.Sp => "Test after schema changes",
            EntityType.View => "View results may change",
            EntityType.Function => "Function behavior may change",
            _ => "Validate during testing"
        };
    }

    // -----------------------------
    // Summary (unchanged)
    // -----------------------------

    private static string BuildSummary(RiskLevel risk, List<VerdictReason> reasons)
    {
        if (reasons.Count == 0)
            return "No significant impact detected";

        return $"{risk} risk – {reasons[0].Statement}";
    }

    // -----------------------------
    // Limitations (unchanged - already correct)
    // -----------------------------

    private static List<string> DetectLimitations(ImpactResult analysis)
    {
        var limits = new List<string>();

        if (analysis.IsTruncated)
            limits.Add("Analysis was truncated due to dependency explosion");

        if (analysis.MaxDepthReached > 3)
            limits.Add("Deep dependency chains detected; indirect effects may exist");

        if (!analysis.EntityImpacts.Any())
            limits.Add("No entity impacts found; metadata may be incomplete");

        return limits;
    }
}