// Infrastructure/Data/Sql/UserSqlQueries.cs
public static class UserSqlQueries
{
    public const string GetById = @"
        SELECT id, email, first_name, last_name, created_at, updated_at
        FROM users 
        WHERE id = @Id AND deleted_at IS NULL";

    public const string GetByEmail = @"
        SELECT id, email, first_name, last_name, created_at, updated_at
        FROM users 
        WHERE email = @Email AND deleted_at IS NULL";

    public const string GetAll = @"
        SELECT id, email, first_name, last_name, created_at, updated_at
        FROM users 
        WHERE deleted_at IS NULL 
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset";

    public const string GetCount = @"
        SELECT COUNT(*) 
        FROM users 
        WHERE deleted_at IS NULL";

    public const string Insert = @"
        INSERT INTO users (id, email, first_name, last_name, created_at)
        VALUES (@Id, @Email, @FirstName, @LastName, @CreatedAt)
        RETURNING id, email, first_name, last_name, created_at, updated_at";

    public const string Update = @"
        UPDATE users 
        SET first_name = @FirstName, 
            last_name = @LastName, 
            updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL";

    public const string SoftDelete = @"
        UPDATE users 
        SET deleted_at = @DeletedAt
        WHERE id = @Id AND deleted_at IS NULL";

    public const string HardDelete = @"
        DELETE FROM users 
        WHERE id = @Id";
}