using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.RoleService;

public interface IRoleService
{
    Task<IEnumerable<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleWithPermissions?> GetRoleWithPermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task<Role> CreateRoleAsync(CreateRoleRequest request, int createdBy, CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(UpdateRoleRequest request, int updatedBy, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default);
    Task UpdateRolePermissionsAsync(int roleId, List<int> permissionIds, int updatedBy, CancellationToken cancellationToken = default);
}

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IUserRepository userRepository,
        ILogger<RoleService> logger)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _roleRepository.GetAllAsync(cancellationToken);
    }

    public async Task<RoleWithPermissions?> GetRoleWithPermissionsAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role == null) return null;

        var permissions = await _roleRepository.GetRolePermissionsAsync(roleId, cancellationToken);

        return new RoleWithPermissions
        {
            Role = role,
            Permissions = permissions.ToList()
        };
    }

    public async Task<Role> CreateRoleAsync(
        CreateRoleRequest request,
        int createdBy,
        CancellationToken cancellationToken = default)
    {
        // Check for duplicate role name
        var existing = await _roleRepository.GetByNameAsync(request.RoleName, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Role '{request.RoleName}' already exists");

        var role = new Role
        {
            RoleName = request.RoleName,
            Description = request.Description,
            IsSystem = false,
            IsActive = true,
            CreatedBy = createdBy
        };

        // Create role and assign permissions atomically
        var permissionsToAssign = request.PermissionIds.Count > 0 ? request.PermissionIds : new List<int>();

        var createdRole = await _roleRepository.CreateRoleWithPermissionsAsync(
            role,
            permissionsToAssign,
            createdBy,
            cancellationToken);

        _logger.LogInformation("Created role {RoleName} with {Count} permissions", createdRole.RoleName, permissionsToAssign.Count);
        return createdRole;
    }

    public async Task UpdateRoleAsync(
        UpdateRoleRequest request,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
            throw new InvalidOperationException($"Role {request.RoleId} not found");

        if (role.IsSystem)
            throw new InvalidOperationException("Cannot modify system roles");

        // Check for duplicate role name (excluding current role)
        var existingWithName = await _roleRepository.GetByNameAsync(request.RoleName, cancellationToken);
        if (existingWithName != null && existingWithName.RoleId != request.RoleId)
            throw new InvalidOperationException($"Role '{request.RoleName}' already exists");

        role.RoleName = request.RoleName;
        role.Description = request.Description;
        role.IsActive = request.IsActive;
        role.UpdatedBy = updatedBy;



        // Update role and permissions atomically
        await _roleRepository.UpdateRoleWithPermissionsAsync(
            role,
            request.PermissionIds,
            updatedBy,
            cancellationToken);

        _logger.LogInformation("Updated role {RoleName} (ID: {RoleId})", role.RoleName, role.RoleId);
    }

    public async Task DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role == null)
            throw new InvalidOperationException($"Role {roleId} not found");

        if (role.IsSystem)
            throw new InvalidOperationException("Cannot delete system roles");

        // Delete role and remove from users atomically
        await _roleRepository.DeleteRoleWithUsersAsync(roleId, _userRepository, cancellationToken);
        
        _logger.LogInformation("Deleted role {RoleName} (ID: {RoleId}) and updated associated users", role.RoleName, roleId);
    }

    public async Task UpdateRolePermissionsAsync(
        int roleId,
        List<int> permissionIds,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        await _roleRepository.UpdateRolePermissionsAtomicAsync(
            roleId,
            permissionIds,
            updatedBy,
            cancellationToken);

        _logger.LogInformation(
            "Updated permissions for role {RoleId}. Assigned {Count} permissions",
            roleId,
            permissionIds.Count);
    }
}
