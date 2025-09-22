using ActoX.Application.Interfaces;
using ActoX.Application.Services;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Application Services
        services.AddScoped<IAuthService, AuthService>();

        // Add other application services here as they're created
        // services.AddScoped<IUserService, UserService>();
        // services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}