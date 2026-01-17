namespace ActoEngine.WebApi.Features.Roles;

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

public class Permission
{
    public int PermissionId { get; set; }
    public string PermissionKey { get; set; } = default!;
    public string Resource { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RoleWithPermissions
{
    public Role Role { get; set; } = default!;
    public List<Permission> Permissions { get; set; } = [];
}





public class PermissionGroupDto
{
    public string Category { get; set; } = default!;
    public List<Permission> Permissions { get; set; } = [];
}
