using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.SqlQueries;

namespace ActoEngine.WebApi.Repositories;

public interface IPermissionRepository
{
    Task<IEnumerable<Permission>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Permission?> GetByIdAsync(int permissionId, CancellationToken cancellationToken = default);
    Task<Permission?> GetByKeyAsync(string permissionKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<PermissionGroupDto>> GetGroupedByCategoryAsync(CancellationToken cancellationToken = default);
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

    public async Task<IEnumerable<PermissionGroupDto>> GetGroupedByCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        var permissions = await QueryAsync<Permission>(
            PermissionQueries.GetGroupedByCategory,
            cancellationToken: cancellationToken);

        return permissions
            .GroupBy(p => p.Category ?? "General")
            .Select(g => new PermissionGroupDto
            {
                Category = g.Key,
                Permissions = [.. g]
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
