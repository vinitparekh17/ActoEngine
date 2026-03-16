using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ActoEngine.WebApi.Features.Auth;

[ApiController]
[Route("api/auth/extension")]
public class ExtensionAuthController(
    IExtensionAuthService extensionAuthService,
    ILogger<ExtensionAuthController> logger) : ControllerBase
{
    [HttpGet("authorize")]
    [Authorize]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery(Name = "code_challenge")] string codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string codeChallengeMethod = "S256",
        [FromQuery] string? state = null,
        CancellationToken ct = default)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated."));
        }

        try
        {
            var code = await extensionAuthService.CreateAuthorizationCodeAsync(
                userId.Value,
                clientId,
                redirectUri,
                codeChallenge,
                codeChallengeMethod,
                state,
                ct);

            var redirect = QueryHelpers.AddQueryString(redirectUri, new Dictionary<string, string?>
            {
                ["code"] = code,
                ["state"] = state
            });

            return Redirect(redirect);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Extension authorize failed for user {UserId}", userId);
            return BadRequest(ApiResponse<object>.Failure("Extension authorization failed.", [ex.Message]));
        }
    }

    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ExtensionTokenResponse>>> ExchangeToken(
        [FromBody] ExtensionTokenExchangeRequest request,
        CancellationToken ct)
    {
        try
        {
            var token = await extensionAuthService.ExchangeCodeAsync(request, ct);
            return Ok(ApiResponse<ExtensionTokenResponse>.Success(token, "Extension access token issued."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Extension token exchange failed.");
            return Unauthorized(ApiResponse<ExtensionTokenResponse>.Failure("Token exchange failed.", [ex.Message]));
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ExtensionTokenResponse>>> RefreshToken(
        [FromBody] ExtensionRefreshRequest request,
        CancellationToken ct)
    {
        try
        {
            var token = await extensionAuthService.RefreshAsync(request, ct);
            return Ok(ApiResponse<ExtensionTokenResponse>.Success(token, "Extension token refreshed."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Extension refresh failed.");
            return Unauthorized(ApiResponse<ExtensionTokenResponse>.Failure("Token refresh failed.", [ex.Message]));
        }
    }
}
