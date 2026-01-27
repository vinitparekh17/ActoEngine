namespace ActoEngine.WebApi.Features.Projects.Dtos.Responses;

/// <summary>
/// Lightweight DTO for project member dropdown binding.
/// </summary>
public class ProjectMemberDto
{
    public int UserId { get; set; }
    public string? FullName { get; set; }
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
}
