using System.Security.Claims;
using System.Security.Cryptography;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task<AuthResult> RefreshSessionAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task LogoutByUserIdAsync(int userId);
    ClaimsPrincipal? ValidateAccessToken(string accessToken);
    Task<TokenRotationResult?> RotateTokensAsync(string currentAccessToken, string refreshToken);

    Task<User?> GetUserAsync(int userId);
}

public class AuthService(
    IUserRepository userRepository,
    ITokenRepository tokenRepository,
    IPasswordHasher passwordHasher,
    ITokenHasher tokenHasher,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ITokenRepository _tokenRepository = tokenRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ITokenHasher _tokenHasher = tokenHasher;
    private readonly ILogger<AuthService> _logger = logger;

    public async Task<User?> GetUserAsync(int userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            var user = await _userRepository.GetByUserNameAsync(username);
            if (user == null || !_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed for username: {Username}", username);
                return new AuthResult { Success = false, ErrorMessage = "Invalid credentials" };
            }

            // Delete existing tokens for this user (single session)
            await _tokenRepository.DeleteByUserIdAsync(user.UserID);

            var accessToken = GenerateToken();
            var refreshToken = GenerateToken();
            var accessExpiry = DateTime.UtcNow.AddMinutes(15); // Shorter for access tokens
            var refreshExpiry = DateTime.UtcNow.AddDays(7);

            await _tokenRepository.StoreTokensAsync(
                user.UserID,
                _tokenHasher.HashToken(accessToken),
                _tokenHasher.HashToken(refreshToken),
                accessExpiry,
                refreshExpiry);

            _logger.LogInformation("User {UserId} logged in successfully", user.UserID);

            return new AuthResult
            {
                Success = true,
                SessionToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = accessExpiry,
                UserId = user.UserID
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username: {Username}", username);
            return new AuthResult { Success = false, ErrorMessage = "Login failed" };
        }
    }

    public async Task<AuthResult> RefreshSessionAsync(string refreshToken)
    {
        try
        {
            var refreshHash = _tokenHasher.HashToken(refreshToken);
            var tokenRecord = await _tokenRepository.GetByRefreshTokenHashAsync(refreshHash);

            if (tokenRecord == null || tokenRecord.RefreshExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token validation failed - token not found or expired");
                return new AuthResult { Success = false, ErrorMessage = "Invalid or expired refresh token" };
            }

            var newAccessToken = GenerateToken();
            var newRefreshToken = GenerateToken();
            var newAccessExpiry = DateTime.UtcNow.AddMinutes(15);
            var newRefreshExpiry = DateTime.UtcNow.AddDays(7);

            await _tokenRepository.RotateTokensAsync(
                tokenRecord.UserID,
                _tokenHasher.HashToken(newAccessToken),
                _tokenHasher.HashToken(newRefreshToken),
                newAccessExpiry,
                newRefreshExpiry);

            _logger.LogInformation("Tokens rotated successfully for user {UserId}", tokenRecord.UserID);

            return new AuthResult
            {
                Success = true,
                SessionToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = newAccessExpiry,
                UserId = tokenRecord.UserID
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return new AuthResult { Success = false, ErrorMessage = "Token refresh failed" };
        }
    }

    public async Task<TokenRotationResult?> RotateTokensAsync(string currentAccessToken, string refreshToken)
    {
        var result = await RefreshSessionAsync(refreshToken);
        if (!result.Success || result.SessionToken == null)
            return null;

        return new TokenRotationResult
        {
            SessionToken = result.SessionToken,
            RefreshToken = result.RefreshToken,
            AccessExpiresAt = result.ExpiresAt,
            RefreshExpiresAt = DateTime.UtcNow.AddDays(7),
            UserId = result.UserId ?? 0
        };
    }

    public ClaimsPrincipal? ValidateAccessToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogDebug("Access token is null or empty");
            return null;
        }

        try
        {
            var tokenHash = _tokenHasher.HashToken(accessToken);
            var tokenRecord = _tokenRepository.GetBySessionTokenHashAsync(tokenHash).GetAwaiter().GetResult();

            if (tokenRecord == null)
            {
                _logger.LogDebug("Access token not found in database");
                return null;
            }

            if (tokenRecord.SessionExpiresAt < DateTime.UtcNow)
            {
                _logger.LogDebug("Access token expired at {ExpiryTime}", tokenRecord.SessionExpiresAt);
                return null;
            }

            var claims = new[]
            {
                    new Claim("sub", tokenRecord.UserID.ToString()),
                    new Claim("user_id", tokenRecord.UserID.ToString()),
                    new Claim("token_type", "access"),
                    new Claim("exp", ((DateTimeOffset)tokenRecord.SessionExpiresAt).ToUnixTimeSeconds().ToString()),
                    new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
                };

            var identity = new ClaimsIdentity(claims, "custom_token");
            var principal = new ClaimsPrincipal(identity);

            _logger.LogDebug("Access token validated successfully for user {UserId}", tokenRecord.UserID);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed for token hash");
            return null;
        }
    }

    public async Task LogoutAsync(string refreshToken)
    {
        try
        {
            var refreshHash = _tokenHasher.HashToken(refreshToken);
            await _tokenRepository.DeleteByRefreshTokenHashAsync(refreshHash);
            _logger.LogInformation("User logged out successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            throw;
        }
    }

    public async Task LogoutByUserIdAsync(int userId)
    {
        try
        {
            await _tokenRepository.DeleteByUserIdAsync(userId);
            _logger.LogInformation("User {UserId} logged out successfully", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user {UserId}", userId);
            throw;
        }
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}
