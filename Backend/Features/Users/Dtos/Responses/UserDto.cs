namespace ActoEngine.WebApi.Features.Users.Dtos.Responses;

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = default!;
    public string? FullName { get; set; }
    public bool IsActive { get; set; }
    public string? RoleName { get; set; }
    public int? RoleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

