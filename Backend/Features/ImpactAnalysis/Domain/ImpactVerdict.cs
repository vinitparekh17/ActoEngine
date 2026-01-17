namespace ActoEngine.WebApi.Features.ImpactAnalysis.Domain
{
    public class ImpactVerdict
    {
        // === Core Decision (what users read first) ===
        public RiskLevel Risk { get; set; }
        public bool RequiresApproval { get; set; }
        public required string Summary { get; set; }  // One-sentence TL;DR

        // === Ranked Explanations ===
        public required List<VerdictReason> Reasons { get; set; }

        // === Uncertainty Flags ===
        public required List<string> Limitations { get; set; }  // Simple strings, not a complex object

        // === Metadata ===
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class VerdictReason
    {
        public int Priority { get; set; }           // 1 = most important
        public required string Statement { get; set; }       // "3 stored procedures read from this table"
        public required string Implication { get; set; }     // "Test these procedures after deployment"
        public List<string> Evidence { get; set; } = new();  // ["SP_GetUsers", "SP_GetOrders", "SP_Dashboard"]
    }

    public enum RiskLevel
    {
        Unknown,  // Not enough data
        Low,      // < 5 dependencies, read-only
        Medium,   // 5-10 dependencies OR has write operations
        High,     // 10+ dependencies OR dangerous patterns (triggers, cascades)
        Critical  // Triggers + writes, or core system tables
    }
}