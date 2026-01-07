namespace ActoEngine.WebApi.Features.Users.Dtos.Responses;

public class UserWithRoleDto
{
    public required UserDto User { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

