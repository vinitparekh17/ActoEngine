namespace ActoEngine.WebApi.Features.Users.Dtos.Responses;

public class UserWithPermissionsDto
{
    public UserDto User { get; set; } = default!;
    public List<string> Permissions { get; set; } = [];
}

