namespace ActoEngine.WebApi.Features.Users;
public static class UserSqlQueries
{
    public const string CheckTableExists = @"
        SELECT CASE 
            WHEN OBJECT_ID('dbo.Users', 'U') IS NOT NULL THEN CAST(1 AS BIT) 
            ELSE CAST(0 AS BIT) 
        END";

    public const string GetById = @"
        SELECT UserID, Username, PasswordHash, FullName, IsActive, Role, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Users 
        WHERE UserID = @UserID";

    public const string GetByUserName = @"
        SELECT UserID, Username, PasswordHash, FullName, IsActive, Role, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Users 
        WHERE Username = @Username";

    public const string GetAll = @"
        SELECT UserID, Username, PasswordHash, FullName, IsActive, Role, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Users 
        ORDER BY CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

    public const string GetCount = @"
        SELECT COUNT(*) 
        FROM Users";

    public const string Insert = @"
        INSERT INTO Users (Username, PasswordHash, FullName, Role, CreatedAt, CreatedBy)
        OUTPUT INSERTED.UserID, INSERTED.Username, INSERTED.PasswordHash, INSERTED.FullName, INSERTED.IsActive, INSERTED.Role, INSERTED.CreatedAt, INSERTED.CreatedBy, INSERTED.UpdatedAt, INSERTED.UpdatedBy
        VALUES (@Username, @PasswordHash, @FullName, @Role, @CreatedAt, @CreatedBy)";

    public const string Update = @"
        UPDATE Users 
        SET FullName = @FullName,
            Role = @Role,
            IsActive = @IsActive,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE UserID = @UserID";

    public const string UpdatePassword = @"
        UPDATE Users
        SET PasswordHash = @PasswordHash,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE UserID = @UserID";

    public const string UpdatePassword = @"
        UPDATE Users
        SET PasswordHash = @PasswordHash,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE UserID = @UserID";

    public const string HardDelete = @"
        DELETE FROM Users
        WHERE UserID = @UserID";

    // New queries for User Management with Roles
    public const string GetAllWithRoles = @"
        SELECT u.UserID, u.Username, u.FullName, u.IsActive,
               u.CreatedAt, u.UpdatedAt, u.RoleId,
               r.RoleName
        FROM Users u
        LEFT JOIN Roles r ON u.RoleId = r.RoleId
        ORDER BY u.CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

    public const string GetUserWithRole = @"
        SELECT u.UserID, u.Username, u.FullName, u.IsActive,
               u.CreatedAt, u.UpdatedAt, u.RoleId,
               r.RoleName
        FROM Users u
        LEFT JOIN Roles r ON u.RoleId = r.RoleId
        WHERE u.UserID = @UserId";

    public const string UpdateRole = @"
        UPDATE Users
        SET RoleId = @RoleId,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE UserID = @UserId";

    public const string InsertWithRole = @"
        INSERT INTO Users (Username, PasswordHash, FullName, RoleId, CreatedAt, CreatedBy)
        OUTPUT INSERTED.UserID, INSERTED.Username, INSERTED.FullName, INSERTED.IsActive,
               INSERTED.RoleId, INSERTED.CreatedAt, INSERTED.UpdatedAt
        VALUES (@Username, @PasswordHash, @FullName, @RoleId, @CreatedAt, @CreatedBy)";

    public const string UpdateUserManagement = @"
        UPDATE Users
        SET FullName = @FullName,
            RoleId = @RoleId,
            IsActive = @IsActive,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE UserID = @UserId";
    public const string UpdateRoleForUsers = @"
        UPDATE Users
        SET RoleId = NULL,
            Role = 'User', -- Default role string
            UpdatedAt = GETUTCDATE()
        WHERE RoleId = @RoleId";
}