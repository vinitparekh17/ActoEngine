namespace ActoEngine.WebApi.Models;

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

public class RoleWithPermissions
{
    public Role Role { get; set; } = default!;
    public List<Permission> Permissions { get; set; } = [];
}

public class CreateRoleRequest
{
    public string RoleName { get; set; } = default!;
    public string? Description { get; set; }
    public List<int> PermissionIds { get; set; } = [];
}

public class UpdateRoleRequest
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<int> PermissionIds { get; set; } = [];
}
