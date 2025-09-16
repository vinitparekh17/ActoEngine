namespace ActoX.Infrastructure.Data.Sql;
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

    public const string HardDelete = @"
        DELETE FROM Users 
        WHERE UserID = @UserID";
}