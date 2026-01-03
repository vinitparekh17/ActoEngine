using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models.Requests.Auth;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "RefreshToken is required")]
    public string RefreshToken { get; set; } = default!;
}

