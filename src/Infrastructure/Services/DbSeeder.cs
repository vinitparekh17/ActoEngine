using System.Reflection;
using ActoX.Application.Interfaces;
using ActoX.Domain.Entities;
using ActoX.Domain.Interfaces;
using ActoX.Infrastructure.Configuration;
using ActoX.Infrastructure.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActoX.Infrastructure.Services;
public class DatabaseSeeder(
    IDbConnectionFactory connectionFactory,
    IPasswordHasher passwordHasher,
    IUserRepository userRepository,
    IOptions<DatabaseSeedingOptions> seedingOptions,
    ILogger<DatabaseSeeder> logger) : IDataSeeder
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly DatabaseSeedingOptions _seedingOptions = seedingOptions.Value;
    private readonly ILogger<DatabaseSeeder> _logger = logger;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_seedingOptions.Enabled)
        {
            _logger.LogInformation("Database seeding is disabled");
            return;
        }

        _logger.LogInformation("Starting database seeding");

        try
        {
            await SeedSharedDataAsync(cancellationToken);
            await SeedAdminUserAsync(cancellationToken);
            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database seeding failed");
            throw;
        }
    }

    public void SeedDevelopmentData(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("No development data to seed");
    }

    public void SeedProductionData(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("No additional production data to seed");
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding admin user...");

        var existingAdmin = await _userRepository.GetByUserNameAsync(_seedingOptions.AdminUser.Username, cancellationToken);
        if (existingAdmin != null)
        {
            _logger.LogInformation("Admin user already exists, skipping creation");
            return;
        }

        var hashedPassword = _passwordHasher.HashPassword(_seedingOptions.DefaultPasswords.AdminUser);
        var adminUser = new User(
            username: _seedingOptions.AdminUser.Username,
            passwordHash: hashedPassword,
            fullName: _seedingOptions.AdminUser.FullName,
            role: _seedingOptions.AdminUser.Role,
            createdBy: "System.Seeder"
        );

        await _userRepository.AddAsync(adminUser, cancellationToken);
        _logger.LogInformation("Admin user created: {Username}", _seedingOptions.AdminUser.Username);
        _logger.LogWarning("IMPORTANT: Change the admin password after first login!");
    }

    private async Task SeedSharedDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding shared data...");

        const string scriptName = "ActoX.Infrastructure.Data.Scripts.SeedData.Shared.SystemSettings.sql";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        try
        {
            var script = GetEmbeddedScript(scriptName);
            if (!string.IsNullOrEmpty(script))
            {
                var batches = script.Split(["\nGO\n", "\nGO\r\n"],
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var batch in batches)
                {
                    if (!string.IsNullOrWhiteSpace(batch))
                    {
                        await connection.ExecuteAsync(batch);
                    }
                }

                _logger.LogInformation("Successfully executed seed script: {ScriptName}", scriptName);
            }
            else
            {
                _logger.LogWarning("Seed script not found: {ScriptName}", scriptName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute seed script: {ScriptName}", scriptName);
        }
    }

    private string? GetEmbeddedScript(string scriptName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(scriptName);

        if (stream == null)
        {
            _logger.LogWarning("Embedded script not found: {ScriptName}", scriptName);
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public Task SeedDevelopmentDataAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SeedProductionDataAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}