using ActoX.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace ActoX.Api.Configuration
{
    public class CustomTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CustomTokenAuthenticationHandler> _logger;

        public CustomTokenAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IServiceScopeFactory scopeFactory)
            : base(options, logger, encoder)
        {
            _scopeFactory = scopeFactory;
            _logger = logger.CreateLogger<CustomTokenAuthenticationHandler>();
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                _logger.LogDebug("No Authorization header found for path: {Path}", Request.Path);
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            const string bearerPrefix = "Bearer ";
            var token = authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                ? authHeader[bearerPrefix.Length..].Trim()
                : authHeader.Trim();

            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            var principal = authService.ValidateAccessToken(token);
            if (principal == null)
            {
                _logger.LogWarning("Token validation failed for path: {Path}", Request.Path);
                return Task.FromResult(AuthenticateResult.Fail("Invalid or expired access token"));
            }

            _logger.LogInformation("User authenticated: {UserId}", principal.FindFirst("user_id")?.Value);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}