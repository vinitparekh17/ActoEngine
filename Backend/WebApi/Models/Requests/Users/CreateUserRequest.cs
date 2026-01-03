namespace ActoEngine.WebApi.Models.Requests.Users;

public class CreateUserRequest
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string? FullName { get; set; }
    public int RoleId { get; set; }
}

