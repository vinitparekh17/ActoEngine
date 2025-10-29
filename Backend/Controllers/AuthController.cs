using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Auth;

namespace ActoEngine.WebApi.Controllers
{
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
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

            var result = await _authService.LoginAsync(request.Username, request.Password);

            if (!result.Success)
                return Unauthorized(ApiResponse<object>.Failure(result.ErrorMessage ?? "Authentication failed"));

            var user = await _authService.GetUserAsync(result.UserId ?? 0);
            if (user == null)
                return Unauthorized(ApiResponse<object>.Failure("User not found"));

            user.PasswordHash = string.Empty;
            var responseData = new AuthTokenResponse
            {
                Token = result.SessionToken!,
                RefreshToken = result.RefreshToken!,
                User = user,
                ExpiresAt = result.ExpiresAt
            };

            // Set refresh token as HttpOnly cookie for automatic refresh
            SetRefreshTokenCookie(result.RefreshToken!, DateTime.UtcNow.AddDays(7));

            return Ok(ApiResponse<AuthTokenResponse>.Success(responseData, "Login successful"));
        }

        /// <summary>
        /// Refreshes the session token using a valid refresh token,
        /// It allows to obtain a new session token without re-authenticating
        /// why is this important: Session tokens typically have a short lifespan for security reasons.
        /// By using a refresh token, clients can seamlessly obtain a new session token without requiring 
        /// the user to log in again, enhancing user experience while maintaining security.
        /// frontend has to call this endpoint before session token expires and has to know when it expires,
        /// these info is provided in the login response and refresh response
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("refresh")]
        [EnableRateLimiting("AuthRateLimit")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

            var result = await _authService.RefreshSessionAsync(request.RefreshToken);

            if (!result.Success)
                return Unauthorized(ApiResponse<object>.Failure(result.ErrorMessage ?? "Token refresh failed"));

            var responseData = new AuthTokenResponse
            {
                Token = result.SessionToken!,
                RefreshToken = result.RefreshToken!,
                ExpiresAt = result.ExpiresAt
            };

            // Note: Refresh token cookie is not updated here since we keep the same refresh token
            // with its original expiration for security reasons

            return Ok(ApiResponse<AuthTokenResponse>.Success(responseData, "Token refreshed successfully"));
        }

        /// <summary>
        /// Logs out the user by invalidating the refresh token
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

            await _authService.LogoutAsync(request.RefreshToken);
            var responseData = new MessageResponse { Message = "Logged out successfully" };

            // Clear refresh token cookie
            ClearRefreshTokenCookie();

            return Ok(ApiResponse<MessageResponse>.Success(responseData, "Logged out successfully"));
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
            var userId = User.FindFirst("user_id")?.Value;
            var tokenType = User.FindFirst("token_type")?.Value;
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

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

        private void SetRefreshTokenCookie(string refreshToken, DateTime expiresAt)
        {
            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAt,
                Path = "/",
                IsEssential = true
            });
        }

        private void ClearRefreshTokenCookie()
        {
            Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }
    }
}