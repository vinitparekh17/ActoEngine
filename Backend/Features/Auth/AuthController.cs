using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Models.Requests.Auth;
using ActoEngine.WebApi.Models.Responses.Auth;
using ActoEngine.Application.Contracts.Auth;
using ActoEngine.WebApi.Services.Auth;
using ActoEngine.Domain.Entities;

namespace ActoEngine.WebApi.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private readonly IAuthService _authService = authService;

    /// <summary>
    /// Authenticates the user and returns session and refresh tokens,
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("login")]
    [EnableRateLimiting("AuthRateLimit")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
        }

        var result = await _authService.LoginAsync(request.Username, request.Password);

        if (!result.Success)
        {
            return Unauthorized(ApiResponse<object>.Failure("Authentication failed"));
        }

        // Guard against unexpected null UserId when authentication succeeds
        if (!result.UserId.HasValue)
        {
            // Log unexpected state - authentication succeeded but UserId is null
            // This indicates a bug in the auth service implementation
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Failure("Authentication failed: unexpected null user ID"));
        }

        var user = await _authService.GetUserAsync(result.UserId.Value);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("Authentication failed"));
        }

        // SECURITY: Map to safe DTO to avoid exposing PasswordHash/PasswordSalt
        var userInfo = new UserBasicInfo
        {
            UserId = user.UserID,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            Permissions = user.Permissions ?? []
        };

        // Set cookies
        SetAccessTokenCookie(result.SessionToken!, result.ExpiresAt);
        SetRefreshTokenCookie(result.RefreshToken!, result.RefreshExpiresAt);

        var responseData = new AuthTokenResponse
        {
            Token = string.Empty, // Token is now in cookie
            RefreshToken = string.Empty, // Token is now in cookie
            User = userInfo,
            ExpiresAt = result.ExpiresAt
        };

        return Ok(ApiResponse<AuthTokenResponse>.Success(responseData, "Login successful"));
    }

    /// <summary>
    /// Refreshes the session token using a valid refresh token from cookie
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("AuthRateLimit")]
    public async Task<IActionResult> Refresh()
    {
        // Try to get refresh token from cookie
        var refreshToken = Request.Cookies["refresh_token"];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(ApiResponse<object>.Failure("No refresh token provided"));
        }

        var result = await _authService.RefreshSessionAsync(refreshToken);

        if (!result.Success)
        {
            // Clear invalid cookies
            ClearAccessTokenCookie();
            ClearRefreshTokenCookie();
            return Unauthorized(ApiResponse<object>.Failure(result.ErrorMessage ?? "Token refresh failed"));
        }

        // Set new access token cookie
        SetAccessTokenCookie(result.SessionToken!, result.ExpiresAt);

        // Extend refresh token cookie expiry (rotation not implemented)
        SetRefreshTokenCookie(result.RefreshToken!, result.RefreshExpiresAt);

        var responseData = new AuthTokenResponse
        {
            Token = string.Empty,
            RefreshToken = string.Empty,
            ExpiresAt = result.ExpiresAt
        };

        return Ok(ApiResponse<AuthTokenResponse>.Success(responseData, "Token refreshed successfully"));
    }

    /// <summary>
    /// Logs out the user by invalidating the refresh token and clearing cookies
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refresh_token"];

        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.LogoutAsync(refreshToken);
        }

        // Clear cookies
        ClearAccessTokenCookie();
        ClearRefreshTokenCookie();

        return Ok(ApiResponse<MessageResponse>.Success(new MessageResponse { Message = "Logged out successfully" }, "Logged out successfully"));
    }


    /// <summary>
    /// Protected route that returns a simple message
    /// </summary>
    /// <returns>Protected message with user information</returns>
    [HttpGet("protected")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ProtectedResourceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public IActionResult GetProtectedMessage()
    {
        // Get user information from the JWT claims
        var userIdClaim = User.FindFirst("user_id")?.Value;
        var tokenType = User.FindFirst("token_type")?.Value;
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

        // Parse userId from claim string to int
        int? userId = int.TryParse(userIdClaim, out var parsedId) ? parsedId : null;

        var responseData = new ProtectedResourceResponse
        {
            Message = "This is protected",
            UserId = userId,
            TokenType = tokenType,
            IsAuthenticated = isAuthenticated,
            AccessTime = DateTime.UtcNow,
            RequestId = HttpContext.TraceIdentifier
        };

        return Ok(ApiResponse<ProtectedResourceResponse>.Success(responseData, "Protected resource accessed successfully"));
    }

    private void SetAccessTokenCookie(string token, DateTime expiresAt)
    {
        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/",
            IsEssential = true
        });
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expiresAt)
    {
        Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/api/Auth", // Restrict refresh token to Auth controller
            IsEssential = true
        });
    }

    private void ClearAccessTokenCookie()
    {
        Response.Cookies.Delete("access_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/Auth"
        });
    }
}