namespace ActoEngine.WebApi.Features.Project.Dtos.Responses;

public class SyncStatus
{
    public required string Status { get; set; }
    public int SyncProgress { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}

