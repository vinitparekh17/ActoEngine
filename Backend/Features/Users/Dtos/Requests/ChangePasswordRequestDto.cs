using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Users.Dtos.Requests;

public class ChangePasswordRequestDto
{
    [Required(ErrorMessage = "New password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [MaxLength(128, ErrorMessage = "Password cannot exceed 128 characters")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = default!;
}

