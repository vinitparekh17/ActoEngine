using Microsoft.AspNetCore.Mvc;
using ActoX.Application.DTOs;
using ActoX.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace ActoX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        private readonly IAuthService _authService = authService;

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(request.Username, request.Password);

            if (!result.Success)
                return Unauthorized(new { message = result.ErrorMessage });

            return Ok(new
            {
                sessionToken = result.SessionToken,
                refreshToken = result.RefreshToken,
                expiresAt = result.ExpiresAt
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RefreshSessionAsync(request.RefreshToken);

            if (!result.Success)
                return Unauthorized(new { message = result.ErrorMessage });

            return Ok(new
            {
                sessionToken = result.SessionToken,
                refreshToken = result.RefreshToken,
                expiresAt = result.ExpiresAt
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _authService.LogoutAsync(request.RefreshToken);
            return Ok(new { message = "Logged out successfully" });
        }


        /// <summary>
        /// Protected route that returns a simple message
        /// </summary>
        /// <returns>Protected message with user information</returns>
        [HttpGet("protected")]
        [Authorize]
        // [ProducesResponseType(typeof(ProtectedResponse), 200)]
        // [ProducesResponseType(typeof(ErrorResponse), 401)]
        public IActionResult GetProtectedMessage()
        {
            // Get user information from the JWT claims
            // var userId = User.FindFirst("user_id")?.Value;
            // var tokenType = User.FindFirst("token_type")?.Value;
            // var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

            // _logger.LogInformation("Protected route accessed by user {UserId}", userId);

            return Ok(new
            {
                Message = "This is protected",
                // UserId = userId,
                // TokenType = tokenType,
                // IsAuthenticated = isAuthenticated,
                // AccessTime = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }
}