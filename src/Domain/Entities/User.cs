namespace ActoX.Domain.Entities;
public class User
{
    public int UserID { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public string? FullName { get; private set; }
    public bool IsActive { get; private set; }
    public string Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Constructor for creating new users
    public User(string username, string passwordHash, string? fullName, string role = "User", string? createdBy = null)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        FullName = fullName;
        Role = role;
        CreatedBy = createdBy;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    // Constructor for database hydration
    public User(int userId, string username, string passwordHash, string? fullName, bool isActive, string role, DateTime createdAt, string? createdBy, DateTime? updatedAt, string? updatedBy)
    {
        UserID = userId;
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

    public void UpdateProfile(string? fullName, string role, string updatedBy)
    {
        FullName = fullName;
        Role = role;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate(string updatedBy)
    {
        IsActive = false;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}