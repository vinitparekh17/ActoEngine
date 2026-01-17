using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using ActoEngine.WebApi.Features.Users;
using System.Data;
using Dapper;

namespace ActoEngine.WebApi.Features.Roles;

public interface IRoleRepository
{
    Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Role?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default);
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);
    Task DeleteAsync(int roleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Permission>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task AddRolePermissionAsync(int roleId, int permissionId, int grantedBy, CancellationToken cancellationToken = default);
    Task RemoveRolePermissionAsync(int roleId, int permissionId, CancellationToken cancellationToken = default);
    Task ClearRolePermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task UpdateRolePermissionsAtomicAsync(int roleId, IEnumerable<int> permissionIds, int updatedBy, CancellationToken cancellationToken = default);
    Task<Role> CreateRoleWithPermissionsAsync(Role role, IEnumerable<int> permissionIds, int createdBy, CancellationToken cancellationToken = default);
    Task DeleteRoleWithUsersAsync(int roleId, IUserRepository userRepository, CancellationToken cancellationToken = default);
    Task UpdateRoleWithPermissionsAsync(Role role, IEnumerable<int> permissionIds, int updatedBy, CancellationToken cancellationToken = default);
}

public class RoleRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<RoleRepository> logger) : BaseRepository(connectionFactory, logger), IRoleRepository
{
    public async Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryAsync<Role>(RoleQueries.GetAll, cancellationToken: cancellationToken);
    }

    public async Task<Role?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await QueryFirstOrDefaultAsync<Role>(
            RoleQueries.GetById,
            new { RoleId = roleId },
            cancellationToken);
    }

    public async Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await QueryFirstOrDefaultAsync<Role>(
            RoleQueries.GetByName,
            new { RoleName = roleName },
            cancellationToken);
    }

    public async Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
        var result = await QueryFirstOrDefaultAsync<Role>(
            RoleQueries.Insert,
            new { role.RoleName, role.Description, role.CreatedBy },
            cancellationToken);

        return result ?? throw new InvalidOperationException("Failed to create role");
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await ExecuteAsync(
            RoleQueries.Update,
            new { role.RoleId, role.RoleName, role.Description, role.IsActive, role.UpdatedBy },
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Role {role.RoleId} not found or is a system role");
        }
    }

    public async Task DeleteAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await ExecuteAsync(
            RoleQueries.Delete,
            new { RoleId = roleId },
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Role {roleId} not found or is a system role");
        }
    }

    public async Task<IEnumerable<Permission>> GetRolePermissionsAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync<Permission>(
            RoleQueries.GetRolePermissions,
            new { RoleId = roleId },
            cancellationToken);
    }

    public async Task AddRolePermissionAsync(
        int roleId,
        int permissionId,
        int grantedBy,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            RoleQueries.AddRolePermission,
            new { RoleId = roleId, PermissionId = permissionId, GrantedBy = grantedBy },
            cancellationToken);
    }

    public async Task RemoveRolePermissionAsync(
        int roleId,
        int permissionId,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            RoleQueries.RemoveRolePermission,
            new { RoleId = roleId, PermissionId = permissionId },
            cancellationToken);
    }

    public async Task ClearRolePermissionsAsync(int roleId, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            RoleQueries.ClearRolePermissions,
            new { RoleId = roleId },
            cancellationToken);
    }

    public async Task UpdateRolePermissionsAtomicAsync(
        int roleId,
        IEnumerable<int> permissionIds,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async (conn, transaction) =>
        {
            await ClearRolePermissionsAsync(roleId, conn, transaction);

            foreach (var permissionId in permissionIds)
            {
                await AddRolePermissionAsync(roleId, permissionId, updatedBy, conn, transaction);
            }

            return true;
        }, cancellationToken);
    }

    public async Task<Role> CreateRoleWithPermissionsAsync(
        Role role,
        IEnumerable<int> permissionIds,
        int createdBy,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async (conn, transaction) =>
        {
            // 1. Create Role
            var createdRole = await conn.QueryFirstOrDefaultAsync<Role>(
                RoleQueries.Insert,
                new { role.RoleName, role.Description, role.CreatedBy },
                transaction) ?? throw new InvalidOperationException("Failed to create role");

            // 2. Assign Permissions
            foreach (var permissionId in permissionIds)
            {
                await AddRolePermissionAsync(createdRole.RoleId, permissionId, createdBy, conn, transaction);
            }

            return createdRole;
        }, cancellationToken);
    }

    public async Task DeleteRoleWithUsersAsync(
        int roleId,
        IUserRepository userRepository,
        CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async (conn, transaction) =>
        {
            await conn.ExecuteAsync(
                UserSqlQueries.UpdateRoleForUsers,
                new { RoleId = roleId },
                transaction);

            var rowsAffected = await conn.ExecuteAsync(
                RoleQueries.Delete,
                new { RoleId = roleId },
                transaction);

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Role {roleId} not found or is a system role");
            }

            return true;
        }, cancellationToken);
    }

    public async Task UpdateRoleWithPermissionsAsync(
        Role role,
        IEnumerable<int> permissionIds,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async (conn, transaction) =>
        {
            var rowsAffected = await conn.ExecuteAsync(
                RoleQueries.Update,
                new { role.RoleId, role.RoleName, role.Description, role.IsActive, role.UpdatedBy },
                transaction);

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Role {role.RoleId} not found or is a system role");
            }

            await ClearRolePermissionsAsync(role.RoleId, conn, transaction);

            foreach (var permissionId in permissionIds)
            {
                await AddRolePermissionAsync(role.RoleId, permissionId, updatedBy, conn, transaction);
            }

            return true;
        }, cancellationToken);
    }

    private async Task ClearRolePermissionsAsync(int roleId, IDbConnection conn, IDbTransaction transaction)
    {
        await conn.ExecuteAsync(
            RoleQueries.ClearRolePermissions,
            new { RoleId = roleId },
            transaction);
    }

    private async Task AddRolePermissionAsync(int roleId, int permissionId, int grantedBy, IDbConnection conn, IDbTransaction transaction)
    {
        await conn.ExecuteAsync(
            RoleQueries.AddRolePermission,
            new { RoleId = roleId, PermissionId = permissionId, GrantedBy = grantedBy },
            transaction);
    }
}
