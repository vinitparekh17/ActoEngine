using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.PermissionService;

public interface IPermissionService
{
    Task<IEnumerable<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<PermissionGroupDto>> GetPermissionsGroupedAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasPermissionAsync(int userId, string permissionKey, CancellationToken cancellationToken = default);
}

public class PermissionService(
    IPermissionRepository permissionRepository,
    ILogger<PermissionService> logger) : IPermissionService
{
    public async Task<IEnumerable<Permission>> GetAllPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await permissionRepository.GetAllAsync(cancellationToken);
    }

    public async Task<IEnumerable<PermissionGroupDto>> GetPermissionsGroupedAsync(
        CancellationToken cancellationToken = default)
    {
        return await permissionRepository.GetGroupedByCategoryAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var permissions = (await permissionRepository.GetUserPermissionsAsync(userId, cancellationToken)).ToList();
        logger.LogDebug("Retrieved {Count} permissions for user {UserId}", permissions.Count, userId);
        return permissions;
    }

    public async Task<bool> UserHasPermissionAsync(
        int userId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        var permissions = await permissionRepository.GetUserPermissionsAsync(userId, cancellationToken);
        return permissions.Contains(permissionKey);
    }
}
