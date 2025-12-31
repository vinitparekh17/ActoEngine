namespace ActoEngine.WebApi.Models;

public class Permission
{
    public int PermissionId { get; set; }
    public string PermissionKey { get; set; } = default!;  // e.g., "Users:Read"
    public string Resource { get; set; } = default!;       // e.g., "Users"
    public string Action { get; set; } = default!;         // e.g., "Read"
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PermissionGroupDto
{
    public string Category { get; set; } = default!;
    public List<Permission> Permissions { get; set; } = [];
}
