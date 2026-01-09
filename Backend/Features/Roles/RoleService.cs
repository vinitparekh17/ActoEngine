using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Features.Users;
using ActoEngine.WebApi.Models.Requests.Role;

namespace ActoEngine.WebApi.Services.RoleService;

public interface IRoleService
{
    Task<IEnumerable<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleWithPermissions?> GetRoleWithPermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task<Role> CreateRoleAsync(CreateRoleRequest request, int createdBy, CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(int roleId, UpdateRoleRequest request, int updatedBy, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default);
    Task UpdateRolePermissionsAsync(int roleId, List<int> permissionIds, int updatedBy, CancellationToken cancellationToken = default);
}

public class RoleService(
    IRoleRepository roleRepository,
    IUserRepository userRepository,
    ILogger<RoleService> logger) : IRoleService
{
    public async Task<IEnumerable<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        return await roleRepository.GetAllAsync(cancellationToken);
    }

    public async Task<RoleWithPermissions?> GetRoleWithPermissionsAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role == null)
        {
            return null;
        }

        var permissions = await roleRepository.GetRolePermissionsAsync(roleId, cancellationToken);

        return new RoleWithPermissions
        {
            Role = role,
            Permissions = [.. permissions]
        };
    }

    public async Task<Role> CreateRoleAsync(
        CreateRoleRequest request,
        int createdBy,
        CancellationToken cancellationToken = default)
    {
        // Check for duplicate role name
        var existing = await roleRepository.GetByNameAsync(request.RoleName, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Role '{request.RoleName}' already exists");
        }

        var role = new Role
        {
            RoleName = request.RoleName,
            Description = request.Description,
            IsSystem = false,
            IsActive = true,
            CreatedBy = createdBy
        };

        // Create role and assign permissions atomically
        var permissionsToAssign = request.PermissionIds.Count > 0 ? request.PermissionIds : [];

        var createdRole = await roleRepository.CreateRoleWithPermissionsAsync(
            role,
            permissionsToAssign,
            createdBy,
            cancellationToken);

        logger.LogInformation("Created role {RoleName} with {Count} permissions", createdRole.RoleName, permissionsToAssign.Count);
        return createdRole;
    }

    public async Task UpdateRoleAsync(
        int roleId,
        UpdateRoleRequest request,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        var role = await roleRepository.GetByIdAsync(roleId, cancellationToken) ?? throw new InvalidOperationException($"Role {roleId} not found");
        if (role.IsSystem)
        {
            throw new InvalidOperationException("Cannot modify system roles");
        }

        // Check for duplicate role name (excluding current role)
        var existingWithName = await roleRepository.GetByNameAsync(request.RoleName, cancellationToken);
        if (existingWithName != null && existingWithName.RoleId != roleId)
        {
            throw new InvalidOperationException($"Role '{request.RoleName}' already exists");
        }

        role.RoleName = request.RoleName;
        role.Description = request.Description;
        role.IsActive = request.IsActive;
        role.UpdatedBy = updatedBy;



        // Update role and permissions atomically
        await roleRepository.UpdateRoleWithPermissionsAsync(
            role,
            request.PermissionIds,
            updatedBy,
            cancellationToken);

        logger.LogInformation("Updated role {RoleName} (ID: {RoleId})", role.RoleName, role.RoleId);
    }

    public async Task DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleRepository.GetByIdAsync(roleId, cancellationToken) ?? throw new InvalidOperationException($"Role {roleId} not found");
        if (role.IsSystem)
        {
            throw new InvalidOperationException("Cannot delete system roles");
        }

        // Delete role and remove from users atomically
        await roleRepository.DeleteRoleWithUsersAsync(roleId, userRepository, cancellationToken);

        logger.LogInformation("Deleted role {RoleName} (ID: {RoleId}) and updated associated users", role.RoleName, roleId);
    }

    public async Task UpdateRolePermissionsAsync(
        int roleId,
        List<int> permissionIds,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        await roleRepository.UpdateRolePermissionsAtomicAsync(
            roleId,
            permissionIds,
            updatedBy,
            cancellationToken);

        logger.LogInformation(
            "Updated permissions for role {RoleId}. Assigned {Count} permissions",
            roleId,
            permissionIds.Count);
    }
}
