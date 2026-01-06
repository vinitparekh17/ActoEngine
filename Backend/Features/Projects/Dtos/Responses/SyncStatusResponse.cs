namespace ActoEngine.WebApi.Features.Project.Dtos.Responses;

public class SyncStatusResponse
{
    public required int ProjectId { get; set; }
    public required string Status { get; set; }
    public int SyncProgress { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}

