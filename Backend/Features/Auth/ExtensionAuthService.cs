using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Auth;

public interface IExtensionAuthService
{
    Task<string> CreateAuthorizationCodeAsync(
        int userId,
        string clientId,
        string redirectUri,
        string codeChallenge,
        string codeChallengeMethod,
        string? state,
        CancellationToken ct = default);

    Task<ExtensionTokenResponse> ExchangeCodeAsync(ExtensionTokenExchangeRequest request, CancellationToken ct = default);
    Task<ExtensionTokenResponse> RefreshAsync(ExtensionRefreshRequest request, CancellationToken ct = default);
    Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string accessToken, CancellationToken ct = default);
}

public class ExtensionAuthService(
    IExtensionAuthRepository extensionAuthRepository,
    ITokenHasher tokenHasher,
    ILogger<ExtensionAuthService> logger) : IExtensionAuthService
{
    private static readonly TimeSpan AuthCodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<string> CreateAuthorizationCodeAsync(
        int userId,
        string clientId,
        string redirectUri,
        string codeChallenge,
        string codeChallengeMethod,
        string? state,
        CancellationToken ct = default)
    {
        if (userId <= 0) throw new InvalidOperationException("Invalid user context.");
        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("client_id is required.");
        if (string.IsNullOrWhiteSpace(redirectUri)) throw new InvalidOperationException("redirect_uri is required.");
        ValidatePkceValue(codeChallenge, "code_challenge");
        if (!codeChallengeMethod.Equals("S256", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only S256 code challenge method is supported.");
        }

        ValidateChromeExtensionRedirectUri(clientId, redirectUri);

        var code = GenerateSecureToken();
        var codeHash = tokenHasher.HashToken(code);
        var now = DateTime.UtcNow;

        await extensionAuthRepository.StoreAuthorizationCodeAsync(new ExtensionAuthCode
        {
            UserID = userId,
            ClientId = clientId.Trim(),
            RedirectUri = redirectUri.Trim(),
            CodeHash = codeHash,
            CodeChallenge = codeChallenge.Trim(),
            CodeChallengeMethod = "S256",
            State = state,
            ExpiresAt = now.Add(AuthCodeLifetime)
        }, ct);

        logger.LogInformation("Issued extension auth code for user {UserId}, client {ClientId}", "[REDACTED]", "[REDACTED]");
        return code;
    }

    public async Task<ExtensionTokenResponse> ExchangeCodeAsync(ExtensionTokenExchangeRequest request, CancellationToken ct = default)
    {
        ValidateExchangeRequest(request);

        var codeHash = tokenHasher.HashToken(request.Code);
        var authCode = await extensionAuthRepository.GetAuthorizationCodeByHashAsync(codeHash, ct)
            ?? throw new InvalidOperationException("Invalid authorization code.");

        if (authCode.ConsumedAt.HasValue)
        {
            throw new InvalidOperationException("Authorization code already used.");
        }
        if (authCode.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Authorization code expired.");
        }
        if (!authCode.ClientId.Equals(request.ClientId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Client ID mismatch.");
        }
        if (!authCode.RedirectUri.Equals(request.RedirectUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Redirect URI mismatch.");
        }
        if (!authCode.CodeChallengeMethod.Equals("S256", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported code challenge method.");
        }

        var computedChallenge = BuildCodeChallenge(request.CodeVerifier);
        if (!computedChallenge.Equals(authCode.CodeChallenge, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid code verifier.");
        }

        var now = DateTime.UtcNow;
        var accessToken = GenerateSecureToken();
        var refreshToken = GenerateSecureToken();

        await extensionAuthRepository.ConsumeCodeAndCreateSessionAsync(authCode.AuthCodeId, new ExtensionTokenSession
        {
            UserID = authCode.UserID,
            ClientId = authCode.ClientId,
            AccessToken = tokenHasher.HashToken(accessToken),
            AccessExpiresAt = now.Add(AccessTokenLifetime),
            RefreshToken = tokenHasher.HashToken(refreshToken),
            RefreshExpiresAt = now.Add(RefreshTokenLifetime)
        }, ct);

        return new ExtensionTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = new DateTimeOffset(now.Add(AccessTokenLifetime)).ToUnixTimeMilliseconds()
        };
    }

    public async Task<ExtensionTokenResponse> RefreshAsync(ExtensionRefreshRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new InvalidOperationException("refreshToken is required.");
        }
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            throw new InvalidOperationException("clientId is required.");
        }

        var refreshHash = tokenHasher.HashToken(request.RefreshToken);
        var session = await extensionAuthRepository.GetSessionByRefreshTokenHashAsync(refreshHash, ct)
            ?? throw new InvalidOperationException("Invalid refresh token.");

        if (session.RevokedAt.HasValue || session.RefreshExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Refresh token expired.");
        }
        if (!session.ClientId.Equals(request.ClientId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Client ID mismatch.");
        }

        var now = DateTime.UtcNow;
        var newAccessToken = GenerateSecureToken();
        var newRefreshToken = GenerateSecureToken();

        var rotated = await extensionAuthRepository.RotateTokenSessionAsync(
            session.SessionId,
            tokenHasher.HashToken(newAccessToken),
            now.Add(AccessTokenLifetime),
            tokenHasher.HashToken(newRefreshToken),
            now.Add(RefreshTokenLifetime),
            session.UpdatedAt,
            ct);

        if (!rotated)
        {
            throw new InvalidOperationException("Could not rotate extension session.");
        }

        return new ExtensionTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = new DateTimeOffset(now.Add(AccessTokenLifetime)).ToUnixTimeMilliseconds()
        };
    }

    public async Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string accessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var hash = tokenHasher.HashToken(accessToken);
            var session = await extensionAuthRepository.GetSessionByAccessTokenHashAsync(hash, ct);
            if (session == null || session.RevokedAt.HasValue || session.AccessExpiresAt <= DateTime.UtcNow)
            {
                return null;
            }

            var claims = new[]
            {
                new Claim("sub", session.UserID.ToString()),
                new Claim("user_id", session.UserID.ToString()),
                new Claim("token_type", "extension_access"),
                new Claim("client_id", session.ClientId),
                new Claim("exp", ((DateTimeOffset)session.AccessExpiresAt).ToUnixTimeSeconds().ToString()),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "extension_token"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to validate extension access token.");
            return null;
        }
    }

    private static void ValidateExchangeRequest(ExtensionTokenExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new InvalidOperationException("code is required.");
        }
        ValidatePkceValue(request.CodeVerifier, "codeVerifier");
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            throw new InvalidOperationException("clientId is required.");
        }
        if (string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            throw new InvalidOperationException("redirectUri is required.");
        }

        ValidateChromeExtensionRedirectUri(request.ClientId, request.RedirectUri);
    }

    private static void ValidateChromeExtensionRedirectUri(string clientId, string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirect))
        {
            throw new InvalidOperationException("redirect_uri must be absolute.");
        }

        if (!redirect.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("redirect_uri must use https.");
        }

        var expectedHost = $"{clientId.Trim()}.chromiumapp.org";
        if (!redirect.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("redirect_uri must match the extension redirect host.");
        }
    }

    private static string GenerateSecureToken()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    private static string BuildCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static void ValidatePkceValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{parameterName} is required.");
        }
        if (value.Length < 43 || value.Length > 128)
        {
            throw new InvalidOperationException($"{parameterName} must be between 43 and 128 characters length.");
        }
        if (!Regex.IsMatch(value, @"^[A-Za-z0-9\-._~]+$"))
        {
            throw new InvalidOperationException($"{parameterName} contains invalid characters.");
        }
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
