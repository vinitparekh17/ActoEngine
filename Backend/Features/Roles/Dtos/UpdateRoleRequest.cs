namespace ActoEngine.WebApi.Models.Requests.Role;

public class UpdateRoleRequest
{
    public string RoleName { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<int> PermissionIds { get; set; } = [];
}
