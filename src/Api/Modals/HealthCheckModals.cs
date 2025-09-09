public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public TimeSpan TotalDuration { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, HealthCheckItem> Checks { get; set; } = new();
    public string? Error { get; set; }
}

public class HealthCheckItem
{
    public string Status { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string?>? Data { get; set; }
    public List<string>? Tags { get; set; }
    public string? Exception { get; set; }
}

public class SimpleHealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class ComponentHealthResponse
{
    public string Component { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string?>? Data { get; set; }
    public List<string>? Tags { get; set; }
    public string? Exception { get; set; }
    public DateTime Timestamp { get; set; }
}