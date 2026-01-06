using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Users.Dtos.Requests;

public class ChangePasswordRequest
{
    [Required]
    public int UserId { get; set; }
    [Required]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters long")]
    [MaxLength(100, ErrorMessage = "New password must be at most 100 characters long")]
    public string NewPassword { get; set; } = default!;
}

