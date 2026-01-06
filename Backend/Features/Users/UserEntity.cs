namespace ActoEngine.WebApi.Features.Users;

public class User
{
    public int UserID { get; set; }
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public byte[] PasswordSalt { get; set; } = default!;
    public string? FullName { get; private set; }
    public bool IsActive { get; private set; }
    public string Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public List<string> Permissions { get; set; } = [];

    // Constructor for creating new users
    public User(string username, string passwordHash, string? fullName = null, string role = "User", string? createdBy = null)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        FullName = fullName;
        IsActive = true;
        Role = role;
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
    }

    // Constructor for database hydration
    public User(int userID, string username, string passwordHash, string? fullName, bool isActive,
                string role, DateTime createdAt, string? createdBy, DateTime? updatedAt = null, string? updatedBy = null)
    {
        UserID = userID;
        Username = username;
        PasswordHash = passwordHash;
        FullName = fullName;
        IsActive = isActive;
        Role = role;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public void UpdateProfile(string? fullName, string? updatedBy = null)
    {
        FullName = fullName;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void ChangePassword(string newPasswordHash, string? updatedBy = null)
    {
        PasswordHash = newPasswordHash ?? throw new ArgumentNullException(nameof(newPasswordHash));
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SetActiveStatus(bool isActive, string? updatedBy = null)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void ChangeRole(string newRole, string? updatedBy = null)
    {
        Role = newRole ?? throw new ArgumentNullException(nameof(newRole));
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}

/// <summary>
/// Lightweight DTO for User information in contexts where only basic user details are needed.
/// Used to avoid materializing the full User entity with sensitive fields like PasswordHash.
/// </summary>
public class UserBasicInfo
{
    public int UserId { get; set; }
    public string Username { get; set; } = default!;
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public List<string> Permissions { get; set; } = [];
}

