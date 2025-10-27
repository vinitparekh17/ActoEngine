using ActoEngine.WebApi.Config;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Auth;
using ActoEngine.WebApi.Sql.Queries;
using Dapper;
using Microsoft.Extensions.Options;

namespace ActoEngine.WebApi.Services.Database;

public interface IDataSeeder
{
    /// <summary>
    /// Seeds all necessary data for the application, including shared data and admin user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    Task SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds development-specific data (not used in this implementation).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    Task SeedDevelopmentDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds production-specific data (not used in this implementation).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    Task SeedProductionDataAsync(CancellationToken cancellationToken = default);
}

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
            await SeedAdminUserAsync(cancellationToken);
            await SeedDefaultDetailsAsync(cancellationToken);
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

    private async Task SeedDefaultDetailsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding default client...");

        using var connection = _connectionFactory.CreateConnection();
        var projectId = await connection.QuerySingleOrDefaultAsync<int>(SeedDataQueries.InsertDefaultProject, new { UserId = 1 });
        if (projectId > 0)
        {
            _logger.LogInformation("Default project ensured with ProjectId: {ProjectId}", projectId);
        }
        else
        {
            _logger.LogWarning("Failed to ensure default project, possibly already exists.");
        }

        var clientId = await connection.QuerySingleOrDefaultAsync<int>(SeedDataQueries.InsertDefaultClient, new { UserId = 1, ProjectId = projectId });
        if (clientId > 0)
        {
            _logger.LogInformation("Default client ensured with ClientId: {ClientId}", clientId);
        }
        else
        {
            _logger.LogWarning("Failed to ensure default client, possibly already exists.");
        }


    }
    // private string? GetEmbeddedScript(string scriptName)
    // {
    //     var assembly = Assembly.GetExecutingAssembly();
    //     using var stream = assembly.GetManifestResourceStream(scriptName);

    //     if (stream == null)
    //     {
    //         _logger.LogWarning("Embedded script not found: {ScriptName}", scriptName);
    //         return null;
    //     }

    //     using var reader = new StreamReader(stream);
    //     return reader.ReadToEnd();
    // }

    public Task SeedDevelopmentDataAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SeedProductionDataAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}