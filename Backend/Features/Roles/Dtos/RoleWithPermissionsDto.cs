namespace ActoEngine.WebApi.Features.Roles.Dtos;

public class RoleWithPermissionsDto
{
    public Role Role { get; set; } = default!;
    public List<Permission> Permissions { get; set; } = [];
}

