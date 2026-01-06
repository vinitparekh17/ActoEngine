namespace ActoEngine.WebApi.Features.Context;

/// <summary>
/// Configuration settings for batch operations
/// </summary>
public class BatchSettings
{
    /// <summary>
    /// Maximum number of entities allowed in a single batch request
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
}
