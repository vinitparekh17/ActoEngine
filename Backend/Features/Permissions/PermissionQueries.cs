namespace ActoEngine.WebApi.Features.Permissions;

public static class PermissionQueries
{
    public const string GetAll = @"
        SELECT PermissionId, PermissionKey, Resource, Action,
               Description, Category, IsActive, CreatedAt
        FROM Permissions
        WHERE IsActive = 1
        ORDER BY Category, Resource, Action";

    public const string GetById = @"
        SELECT PermissionId, PermissionKey, Resource, Action,
               Description, Category, IsActive, CreatedAt
        FROM Permissions
        WHERE PermissionId = @PermissionId";

    public const string GetByKey = @"
        SELECT PermissionId, PermissionKey, Resource, Action,
               Description, Category, IsActive, CreatedAt
        FROM Permissions
        WHERE PermissionKey = @PermissionKey";

    public const string GetGroupedByCategory = @"
        SELECT PermissionId, PermissionKey, Resource, Action,
               Description, Category, IsActive, CreatedAt
        FROM Permissions
        WHERE IsActive = 1
        ORDER BY Category, Resource, Action";

    public const string GetUserPermissions = @"
        SELECT DISTINCT p.PermissionKey
        FROM Permissions p
        INNER JOIN RolePermissions rp ON p.PermissionId = rp.PermissionId
        INNER JOIN Roles r ON rp.RoleId = r.RoleId
        INNER JOIN Users u ON r.RoleId = u.RoleId
        WHERE u.UserID = @UserId
          AND p.IsActive = 1
          AND r.IsActive = 1
          AND u.IsActive = 1";
}
