using ActoEngine.WebApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace ActoEngine.WebApi.Infrastructure.Security;

public static class SecurityExtensions
{
    public static void AddCustomSecurity(this IServiceCollection services, IWebHostEnvironment environment)
    {
        // Configure authentication
        services.AddAuthentication("TokenAuth")
            .AddScheme<AuthenticationSchemeOptions, CustomTokenAuthenticationHandler>("TokenAuth", null);

        services.AddAuthorization();

        // Configure anti-forgery (CSRF protection)
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "XSRF-TOKEN";
            options.Cookie.HttpOnly = false; // Must be false so JavaScript can read it
                                             // SECURITY FIX: Only require HTTPS in production (allows HTTP in development)
            options.Cookie.SecurePolicy = environment.IsProduction()
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        // Configure HSTS for production security
        services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });
    }

    public static void ConfigureProxySettings(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Configure forwarded headers for reverse proxy support
        // SECURITY: Only trust X-Forwarded-* headers from known proxies to prevent IP spoofing
        // and other header injection attacks.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Development defaults: Trust localhost and common Docker networks
            // These are safe for local development but should NOT be used in production
            if (environment.IsDevelopment())
            {
                options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
                options.KnownProxies.Add(IPAddress.Parse("::1"));
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.17.0.0"), 16)); // Docker default bridge
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));     // Common private network
            }

            // Production: Load trusted proxy IPs from environment variable
            var trustedProxies = configuration["TRUSTED_PROXY_IPS"];
            if (!string.IsNullOrEmpty(trustedProxies))
            {
                foreach (var proxy in trustedProxies.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmedProxy = proxy.Trim();

                    // Check if it's a CIDR notation (e.g., 172.16.0.0/16)
                    if (trimmedProxy.Contains('/'))
                    {
                        var parts = trimmedProxy.Split('/');
                        if (parts.Length == 2 &&
                            IPAddress.TryParse(parts[0], out var networkAddress) &&
                            int.TryParse(parts[1], out var prefixLength))
                        {
                            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(networkAddress, prefixLength));
                        }
                    }
                    // Single IP address
                    else if (IPAddress.TryParse(trimmedProxy, out var proxyAddress))
                    {
                        options.KnownProxies.Add(proxyAddress);
                    }
                }
            }

            // If no proxies configured in production, log a warning
            if (!environment.IsDevelopment() &&
                options.KnownProxies.Count == 0 &&
                options.KnownNetworks.Count == 0)
            {
                Console.WriteLine("WARNING: No trusted proxies configured. X-Forwarded headers will not be processed.");
                Console.WriteLine("Set TRUSTED_PROXY_IPS environment variable to configure trusted proxies.");
            }
        });
    }
}