namespace ActoEngine.WebApi.SqlQueries;

public static class RoleQueries
{
    public const string GetAll = @"
        SELECT RoleId, RoleName, Description, IsSystem, IsActive,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Roles
        WHERE IsActive = 1
        ORDER BY RoleName";

    public const string GetById = @"
        SELECT RoleId, RoleName, Description, IsSystem, IsActive,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Roles
        WHERE RoleId = @RoleId";

    public const string GetByName = @"
        SELECT RoleId, RoleName, Description, IsSystem, IsActive,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Roles
        WHERE RoleName = @RoleName";

    public const string Insert = @"
        INSERT INTO Roles (RoleName, Description, IsSystem, CreatedAt, CreatedBy)
        OUTPUT INSERTED.RoleId, INSERTED.RoleName, INSERTED.Description,
               INSERTED.IsSystem, INSERTED.IsActive, INSERTED.CreatedAt,
               INSERTED.CreatedBy, INSERTED.UpdatedAt, INSERTED.UpdatedBy
        VALUES (@RoleName, @Description, 0, GETDATE(), @CreatedBy)";

    public const string Update = @"
        UPDATE Roles
        SET RoleName = @RoleName,
            Description = @Description,
            IsActive = @IsActive,
            UpdatedAt = GETDATE(),
            UpdatedBy = @UpdatedBy
        WHERE RoleId = @RoleId AND IsSystem = 0";

    public const string Delete = @"
        DELETE FROM Roles
        WHERE RoleId = @RoleId AND IsSystem = 0";

    public const string GetRolePermissions = @"
        SELECT p.PermissionId, p.PermissionKey, p.Resource, p.Action,
               p.Description, p.Category, p.IsActive, p.CreatedAt
        FROM Permissions p
        INNER JOIN RolePermissions rp ON p.PermissionId = rp.PermissionId
        WHERE rp.RoleId = @RoleId AND p.IsActive = 1
        ORDER BY p.Category, p.Resource, p.Action";

    public const string AddRolePermission = @"
        IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @RoleId AND PermissionId = @PermissionId)
        BEGIN
            INSERT INTO RolePermissions (RoleId, PermissionId, GrantedAt, GrantedBy)
            VALUES (@RoleId, @PermissionId, GETDATE(), @GrantedBy)
        END";

    public const string RemoveRolePermission = @"
        DELETE FROM RolePermissions
        WHERE RoleId = @RoleId AND PermissionId = @PermissionId";

    public const string ClearRolePermissions = @"
        DELETE FROM RolePermissions
        WHERE RoleId = @RoleId";
}
