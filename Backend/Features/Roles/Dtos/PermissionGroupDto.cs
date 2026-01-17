namespace ActoEngine.WebApi.Features.Roles.Dtos;

public class PermissionGroupDto
{
    public string Category { get; set; } = default!;
    public List<Permission> Permissions { get; set; } = [];
}

