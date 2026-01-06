namespace ActoEngine.WebApi.Models.Responses.Role;

public class PermissionGroupDto
{
    public string Category { get; set; } = default!;
    public List<Domain.Entities.Permission> Permissions { get; set; } = [];
}

