using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Users.Dtos.Requests;

public class CreateUserRequest
{
    [Required(ErrorMessage = "Username is required")]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters long")]
    [MaxLength(50, ErrorMessage = "Username must be at most 50 characters long")]
    public string Username { get; set; } = default!;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    [MaxLength(100, ErrorMessage = "Password must be at most 100 characters long")]
    public string Password { get; set; } = default!;
    public string? FullName { get; set; }
    public int RoleId { get; set; }
}

