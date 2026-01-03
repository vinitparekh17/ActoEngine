namespace ActoEngine.WebApi.Models.Requests.Role;

public class CreateRoleRequest
{
    public string RoleName { get; set; } = default!;
    public string? Description { get; set; }
    public List<int> PermissionIds { get; set; } = [];
}

