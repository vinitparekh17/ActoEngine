using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Projects.Dtos.Requests;

public class VerifyConnectionRequest
{
    [Required(ErrorMessage = "Server is required")]
    [StringLength(255, ErrorMessage = "Server name cannot exceed 255 characters")]
    public string Server { get; set; } = default!;

    [Required(ErrorMessage = "Database name is required")]
    [StringLength(128, ErrorMessage = "Database name cannot exceed 128 characters")]
    public string DatabaseName { get; set; } = default!;

    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
    public string Username { get; set; } = default!;

    /// <summary>
    /// Database password. Marked with [JsonIgnore] to prevent serialization in logs/responses.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, ErrorMessage = "Password cannot exceed 100 characters")]
    public string Password { get; set; } = default!;

    public int Port { get; set; } = 1433; // Default SQL Server port

    [StringLength(50, ErrorMessage = "Database type cannot exceed 50 characters")]
    public string DatabaseType { get; set; } = "SqlServer";

    // Advanced connection options (optional with sensible defaults)

    /// <summary>
    /// Whether to encrypt the connection. Default: true (recommended).
    /// </summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>
    /// Whether to trust the server certificate without validation.
    /// SECURITY: Only set to true for development/testing or when using self-signed certificates.
    /// </summary>
    public bool TrustServerCertificate { get; set; } = false;

    /// <summary>
    /// Connection timeout in seconds. Default: 30. Range: 5-120.
    /// </summary>
    [Range(5, 120, ErrorMessage = "Connection timeout must be between 5 and 120 seconds")]
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// Application name for SQL Server connection tracking. Optional.
    /// </summary>
    [StringLength(128, ErrorMessage = "Application name cannot exceed 128 characters")]
    public string? ApplicationName { get; set; }
}

