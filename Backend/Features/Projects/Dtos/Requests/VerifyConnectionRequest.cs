using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ActoEngine.WebApi.Features.Project.Dtos.Requests;

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
}

