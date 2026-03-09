namespace ActoEngine.WebApi.Features.Projects.Dtos.Requests;

public class DiffQueryRequest
{
    public int ProjectId { get; set; }
    public required string ConnectionString { get; set; }
}
