using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using ActoEngine.WebApi.Features.Roles;

namespace ActoEngine.WebApi.Features.Permissions;

public interface IPermissionRepository
{
    Task<IEnumerable<Permission>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Permission?> GetByIdAsync(int permissionId, CancellationToken cancellationToken = default);
    Task<Permission?> GetByKeyAsync(string permissionKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<Roles.PermissionGroupDto>> GetGroupedByCategoryAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken = default);
}

public class PermissionRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<PermissionRepository> logger) : BaseRepository(connectionFactory, logger), IPermissionRepository
{
    public async Task<IEnumerable<Permission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryAsync<Permission>(PermissionQueries.GetAll, cancellationToken: cancellationToken);
    }

    public async Task<Permission?> GetByIdAsync(int permissionId, CancellationToken cancellationToken = default)
    {
        return await QueryFirstOrDefaultAsync<Permission>(
            PermissionQueries.GetById,
            new { PermissionId = permissionId },
            cancellationToken);
    }

    public async Task<Permission?> GetByKeyAsync(string permissionKey, CancellationToken cancellationToken = default)
    {
        return await QueryFirstOrDefaultAsync<Permission>(
            PermissionQueries.GetByKey,
            new { PermissionKey = permissionKey },
            cancellationToken);
    }

    public async Task<IEnumerable<Roles.PermissionGroupDto>> GetGroupedByCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        var permissions = await QueryAsync<Permission>(
            PermissionQueries.GetGroupedByCategory,
            cancellationToken: cancellationToken);

        return permissions
            .GroupBy(p => p.Category ?? "General")
            .Select(g => new Roles.PermissionGroupDto
            {
                Category = g.Key,
                Permissions = g.Select(p => new Roles.Permission
                {
                    PermissionId = p.PermissionId,
                    PermissionKey = p.PermissionKey,
                    Resource = p.Resource,
                    Action = p.Action,
                    Description = p.Description,
                    Category = p.Category,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                }).ToList()
            });
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync<string>(
            PermissionQueries.GetUserPermissions,
            new { UserId = userId },
            cancellationToken);
    }
}
