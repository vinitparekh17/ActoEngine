namespace ActoEngine.WebApi.Infrastructure.Database;

public static class SeedDataQueries
{
    public const string GetDefaultClientId = @"
        SELECT ClientId
        FROM Clients
        WHERE ClientName = 'Default Client' AND IsActive = 1;";

    public const string InsertDefaultClient = @"
        INSERT INTO Clients (ClientName, IsActive, CreatedAt, CreatedBy)
        VALUES ('Default Client', 1, GETUTCDATE(), @UserId);

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS ClientId;";

    public const string InsertRoles = @"
        IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleName = @RoleName)
        BEGIN
            INSERT INTO Roles (RoleName, Description, IsSystem, IsActive, CreatedAt)
            VALUES (@RoleName, @Description, @IsSystem, 1, GETUTCDATE());
        END";

    public const string InsertPermissions = @"
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionKey = @PermissionKey)
        BEGIN
            INSERT INTO Permissions (PermissionKey, Resource, Action, Description, Category, IsActive, CreatedAt)
            VALUES (@PermissionKey, @Resource, @Action, @Description, @Category, 1, GETUTCDATE());
        END";

    public const string InsertRolePermissions = @"
        DECLARE @RoleId INT = (SELECT RoleId FROM Roles WHERE RoleName = @RoleName);
        DECLARE @PermissionId INT = (SELECT PermissionId FROM Permissions WHERE PermissionKey = @PermissionKey);

        IF @RoleId IS NOT NULL AND @PermissionId IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @RoleId AND PermissionId = @PermissionId)
        BEGIN
            INSERT INTO RolePermissions (RoleId, PermissionId, GrantedAt)
            VALUES (@RoleId, @PermissionId, GETUTCDATE());
        END";

    public const string MigrateUserRoles = @"
        UPDATE Users
        SET RoleId = (SELECT RoleId FROM Roles WHERE Roles.RoleName = Users.Role)
        WHERE RoleId IS NULL AND Role IS NOT NULL;";
}