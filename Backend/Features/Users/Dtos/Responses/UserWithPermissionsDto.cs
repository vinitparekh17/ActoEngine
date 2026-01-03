namespace ActoEngine.WebApi.Models.Responses.Users;

public class UserWithPermissionsDto
{
    public UserDto User { get; set; } = default!;
    public List<string> Permissions { get; set; } = [];
}

