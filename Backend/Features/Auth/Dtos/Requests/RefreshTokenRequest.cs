using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Auth.Dtos.Requests;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "RefreshToken is required")]
    public string RefreshToken { get; set; } = default!;
}

