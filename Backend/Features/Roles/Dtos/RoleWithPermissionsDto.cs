using ActoEngine.Domain.Entities;

namespace ActoEngine.WebApi.Models.Responses.Role;

public class RoleWithPermissionsDto
{
    public Domain.Entities.Role Role { get; set; } = default!;
    public List<Domain.Entities.Permission> Permissions { get; set; } = [];
}

