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

public class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        IPermissionRepository permissionRepository,
        ILogger<PermissionService> logger)
    {
        _permissionRepository = permissionRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Permission>> GetAllPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _permissionRepository.GetAllAsync(cancellationToken);
    }

    public async Task<IEnumerable<PermissionGroupDto>> GetPermissionsGroupedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _permissionRepository.GetGroupedByCategoryAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var permissions = await _permissionRepository.GetUserPermissionsAsync(userId, cancellationToken);
        _logger.LogDebug("Retrieved {Count} permissions for user {UserId}", permissions.Count(), userId);
        return permissions;
    }

    public async Task<bool> UserHasPermissionAsync(
        int userId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        var permissions = await _permissionRepository.GetUserPermissionsAsync(userId, cancellationToken);
        return permissions.Contains(permissionKey);
    }
}
