using ActoEngine.WebApi.Features.Auth;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Security.Cryptography;
using System.Text;

namespace ActoEngine.Tests.Auth;

public class ExtensionAuthServiceTests
{
    private readonly IExtensionAuthRepository _repo = Substitute.For<IExtensionAuthRepository>();
    private readonly TokenHasher _tokenHasher = new();
    private readonly ILogger<ExtensionAuthService> _logger = Substitute.For<ILogger<ExtensionAuthService>>();

    private static string BuildCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithValidVerifier_ReturnsTokensAndConsumesCode()
    {
        var service = new ExtensionAuthService(_repo, _tokenHasher, _logger);
        var code = "auth-code-1";
        var verifier = "code-verifier-1";
        var codeHash = _tokenHasher.HashToken(code);
        const string clientId = "abc";
        const string redirectUri = "https://abc.chromiumapp.org/callback";

        _repo.GetAuthorizationCodeByHashAsync(codeHash, Arg.Any<CancellationToken>())
            .Returns(new ExtensionAuthCode
            {
                AuthCodeId = 3,
                UserID = 7,
                ClientId = clientId,
                RedirectUri = redirectUri,
                CodeHash = codeHash,
                CodeChallenge = BuildCodeChallenge(verifier),
                CodeChallengeMethod = "S256",
                ExpiresAt = DateTime.UtcNow.AddMinutes(3)
            });
        _repo.MarkAuthorizationCodeConsumedAsync(3, Arg.Any<CancellationToken>()).Returns(true);

        var response = await service.ExchangeCodeAsync(new ExtensionTokenExchangeRequest
        {
            Code = code,
            CodeVerifier = verifier,
            ClientId = clientId,
            RedirectUri = redirectUri
        });

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.True(response.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await _repo.Received(1).StoreTokenSessionAsync(Arg.Any<ExtensionTokenSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithVerifierMismatch_Throws()
    {
        var service = new ExtensionAuthService(_repo, _tokenHasher, _logger);
        var code = "auth-code-2";
        var codeHash = _tokenHasher.HashToken(code);
        const string clientId = "abc";
        const string redirectUri = "https://abc.chromiumapp.org/callback";

        _repo.GetAuthorizationCodeByHashAsync(codeHash, Arg.Any<CancellationToken>())
            .Returns(new ExtensionAuthCode
            {
                AuthCodeId = 4,
                UserID = 9,
                ClientId = clientId,
                RedirectUri = redirectUri,
                CodeHash = codeHash,
                CodeChallenge = BuildCodeChallenge("expected-verifier"),
                CodeChallengeMethod = "S256",
                ExpiresAt = DateTime.UtcNow.AddMinutes(3)
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExchangeCodeAsync(new ExtensionTokenExchangeRequest
        {
            Code = code,
            CodeVerifier = "wrong-verifier",
            ClientId = clientId,
            RedirectUri = redirectUri
        }));

        Assert.Equal("Invalid code verifier.", ex.Message);
        await _repo.DidNotReceive().MarkAuthorizationCodeConsumedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_RotatesTokens()
    {
        var service = new ExtensionAuthService(_repo, _tokenHasher, _logger);
        var refreshToken = "refresh-1";
        var refreshHash = _tokenHasher.HashToken(refreshToken);

        _repo.GetSessionByRefreshTokenHashAsync(refreshHash, Arg.Any<CancellationToken>())
            .Returns(new ExtensionTokenSession
            {
                SessionId = 42,
                UserID = 10,
                ClientId = "ext-client",
                AccessToken = "x",
                RefreshToken = refreshHash,
                AccessExpiresAt = DateTime.UtcNow.AddMinutes(1),
                RefreshExpiresAt = DateTime.UtcNow.AddDays(1)
            });
        _repo.RotateTokenSessionAsync(42, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await service.RefreshAsync(new ExtensionRefreshRequest
        {
            RefreshToken = refreshToken,
            ClientId = "ext-client"
        });

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        await _repo.Received(1).RotateTokenSessionAsync(42, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAuthorizationCodeAsync_WithMismatchedRedirectHost_Throws()
    {
        var service = new ExtensionAuthService(_repo, _tokenHasher, _logger);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAuthorizationCodeAsync(
                userId: 7,
                clientId: "abc",
                redirectUri: "https://wrong.chromiumapp.org/callback",
                codeChallenge: "challenge",
                codeChallengeMethod: "S256",
                state: "state"));

        Assert.Equal("redirect_uri must match the extension redirect host.", ex.Message);
        await _repo.DidNotReceive().StoreAuthorizationCodeAsync(Arg.Any<ExtensionAuthCode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_ValidToken_ReturnsPrincipal()
    {
        var service = new ExtensionAuthService(_repo, _tokenHasher, _logger);
        var accessToken = "access-1";
        var accessHash = _tokenHasher.HashToken(accessToken);

        _repo.GetSessionByAccessTokenHashAsync(accessHash, Arg.Any<CancellationToken>())
            .Returns(new ExtensionTokenSession
            {
                SessionId = 5,
                UserID = 321,
                ClientId = "ext-client",
                AccessToken = accessHash,
                AccessExpiresAt = DateTime.UtcNow.AddMinutes(10),
                RefreshToken = "refreshHash",
                RefreshExpiresAt = DateTime.UtcNow.AddDays(1)
            });

        var principal = await service.ValidateAccessTokenAsync(accessToken);

        Assert.NotNull(principal);
        Assert.Equal("321", principal!.FindFirst("user_id")?.Value);
        Assert.Equal("extension_access", principal.FindFirst("token_type")?.Value);
    }
}
