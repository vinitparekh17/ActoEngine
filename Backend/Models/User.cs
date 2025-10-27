using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string Username { get; set; } = default!;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
        public string Password { get; set; } = default!;
    }

    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "RefreshToken is required")]
        public string RefreshToken { get; set; } = default!;
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SessionToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int? UserId { get; set; }
    }

    public class AuthTokenResponse
    {
        public required string Token { get; set; }
        public required string RefreshToken { get; set; }
        public User User { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
    }

    public class ProtectedResourceResponse
    {
        public required string Message { get; set; }
        public string? UserId { get; set; }
        public string? TokenType { get; set; }
        public bool IsAuthenticated { get; set; }
        public DateTime AccessTime { get; set; }
        public required string RequestId { get; set; }
    }


    public class TokenRotationResult
    {
        public string SessionToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTime AccessExpiresAt { get; set; }
        public DateTime RefreshExpiresAt { get; set; }
        public int UserId { get; set; }
    }
}


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