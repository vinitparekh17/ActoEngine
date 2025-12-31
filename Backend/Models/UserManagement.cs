namespace ActoEngine.WebApi.Models;

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

public class CreateUserRequest
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string? FullName { get; set; }
    public int RoleId { get; set; }
}

public class UpdateUserRequest
{
    public int UserId { get; set; }
    public string? FullName { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; }
}

public class ChangePasswordRequest
{
    public int UserId { get; set; }
    public string NewPassword { get; set; } = default!;
}

public class UserWithPermissionsDto
{
    public UserDto User { get; set; } = default!;
    public List<string> Permissions { get; set; } = [];
}
