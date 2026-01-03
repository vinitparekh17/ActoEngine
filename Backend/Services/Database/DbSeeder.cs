using ActoEngine.Domain.Entities;
using ActoEngine.WebApi.Config;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Auth;
using ActoEngine.WebApi.SqlQueries;
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
            await SeedRolesAndPermissionsAsync(cancellationToken);
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

    /// <summary>
    /// Ensures a global default client exists in the database, inserting one with UserId = 1 if none is found.
    /// </summary>
    private async Task SeedDefaultDetailsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding Global Default Client...");

        using var connection = _connectionFactory.CreateConnection();

        // 2. Create or get global default client (not tied to a specific project)
        var existingClientId = await connection.QuerySingleOrDefaultAsync<int?>(SeedDataQueries.GetDefaultClientId);

        if (existingClientId.HasValue)
        {
            _logger.LogInformation("Global default client already exists with ClientId: {ClientId}", existingClientId.Value);
        }
        else
        {
            var admin = await _userRepository.GetByUserNameAsync(_seedingOptions.AdminUser.Username, cancellationToken);
            var createdByUserId = admin?.UserID ?? throw new InvalidOperationException("Admin user must exist before seeding default client");

            var clientId = await connection.QuerySingleAsync<int>(SeedDataQueries.InsertDefaultClient, new { UserId = createdByUserId });
            _logger.LogInformation("Created global default client with ClientId: {ClientId}", clientId);
        }
    }

    private async Task SeedRolesAndPermissionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding Roles and Permissions...");
        using var connection = _connectionFactory.CreateConnection();

        // 1. Seed Roles
        var roles = new[]
        {
            new { RoleName = "Admin", Description = "System administrator with full access", IsSystem = true },
            new { RoleName = "User", Description = "Standard user with limited permissions", IsSystem = true },
            new { RoleName = "Manager", Description = "Manager with most permissions except user management", IsSystem = true },
            new { RoleName = "ReadOnly", Description = "Read-only access to all resources", IsSystem = true }
        };

        foreach (var role in roles)
        {
            await connection.ExecuteAsync(SeedDataQueries.InsertRoles, role);
        }

        // 2. Seed Permissions
        var permissions = new[]
        {
            // User Management
            new { PermissionKey = "Users:Read", Resource = "Users", Action = "Read", Description = "View user information", Category = "User Management" },
            new { PermissionKey = "Users:Create", Resource = "Users", Action = "Create", Description = "Create new users", Category = "User Management" },
            new { PermissionKey = "Users:Update", Resource = "Users", Action = "Update", Description = "Update user information", Category = "User Management" },
            new { PermissionKey = "Users:Delete", Resource = "Users", Action = "Delete", Description = "Delete users", Category = "User Management" },
            new { PermissionKey = "Users:Activate", Resource = "Users", Action = "Activate", Description = "Activate/deactivate users", Category = "User Management" },

            // Role Management
            new { PermissionKey = "Roles:Read", Resource = "Roles", Action = "Read", Description = "View roles and permissions", Category = "Role Management" },
            new { PermissionKey = "Roles:Create", Resource = "Roles", Action = "Create", Description = "Create new roles", Category = "Role Management" },
            new { PermissionKey = "Roles:Update", Resource = "Roles", Action = "Update", Description = "Update existing roles", Category = "Role Management" },
            new { PermissionKey = "Roles:Delete", Resource = "Roles", Action = "Delete", Description = "Delete roles", Category = "Role Management" },
            new { PermissionKey = "Roles:Assign", Resource = "Roles", Action = "Assign", Description = "Assign roles to users", Category = "Role Management" },

            // Project Management
            new { PermissionKey = "Projects:Read", Resource = "Projects", Action = "Read", Description = "View projects", Category = "Project Management" },
            new { PermissionKey = "Projects:Create", Resource = "Projects", Action = "Create", Description = "Create new projects", Category = "Project Management" },
            new { PermissionKey = "Projects:Update", Resource = "Projects", Action = "Update", Description = "Update project information", Category = "Project Management" },
            new { PermissionKey = "Projects:Delete", Resource = "Projects", Action = "Delete", Description = "Delete projects", Category = "Project Management" },
            new { PermissionKey = "Projects:Link", Resource = "Projects", Action = "Link", Description = "Link projects to databases", Category = "Project Management" },

            // Client Management
            new { PermissionKey = "Clients:Read", Resource = "Clients", Action = "Read", Description = "View clients", Category = "Client Management" },
            new { PermissionKey = "Clients:Create", Resource = "Clients", Action = "Create", Description = "Create new clients", Category = "Client Management" },
            new { PermissionKey = "Clients:Update", Resource = "Clients", Action = "Update", Description = "Update client information", Category = "Client Management" },
            new { PermissionKey = "Clients:Delete", Resource = "Clients", Action = "Delete", Description = "Delete clients", Category = "Client Management" },

            // Context Management
            new { PermissionKey = "Contexts:Read", Resource = "Contexts", Action = "Read", Description = "View context information", Category = "Context Management" },
            new { PermissionKey = "Contexts:Create", Resource = "Contexts", Action = "Create", Description = "Create context entries", Category = "Context Management" },
            new { PermissionKey = "Contexts:Update", Resource = "Contexts", Action = "Update", Description = "Update context information", Category = "Context Management" },
            new { PermissionKey = "Contexts:Delete", Resource = "Contexts", Action = "Delete", Description = "Delete context entries", Category = "Context Management" },
            new { PermissionKey = "Contexts:Review", Resource = "Contexts", Action = "Review", Description = "Review and approve context changes", Category = "Context Management" },

            // Schema Management
            new { PermissionKey = "Schema:Read", Resource = "Schema", Action = "Read", Description = "View database schema", Category = "Schema Management" },
            new { PermissionKey = "Schema:Sync", Resource = "Schema", Action = "Sync", Description = "Synchronize database schema", Category = "Schema Management" },

            // Form Builder
            new { PermissionKey = "Forms:Read", Resource = "Forms", Action = "Read", Description = "View form configurations", Category = "Form Builder" },
            new { PermissionKey = "Forms:Create", Resource = "Forms", Action = "Create", Description = "Create new form configurations", Category = "Form Builder" },
            new { PermissionKey = "Forms:Update", Resource = "Forms", Action = "Update", Description = "Update form configurations", Category = "Form Builder" },
            new { PermissionKey = "Forms:Delete", Resource = "Forms", Action = "Delete", Description = "Delete form configurations", Category = "Form Builder" },
            new { PermissionKey = "Forms:Generate", Resource = "Forms", Action = "Generate", Description = "Generate forms from configurations", Category = "Form Builder" },

            // SP Builder
            new { PermissionKey = "StoredProcedures:Read", Resource = "StoredProcedures", Action = "Read", Description = "View stored procedures", Category = "SP Builder" },
            new { PermissionKey = "StoredProcedures:Create", Resource = "StoredProcedures", Action = "Create", Description = "Create new stored procedures", Category = "SP Builder" },
            new { PermissionKey = "StoredProcedures:Update", Resource = "StoredProcedures", Action = "Update", Description = "Update stored procedures", Category = "SP Builder" },
            new { PermissionKey = "StoredProcedures:Delete", Resource = "StoredProcedures", Action = "Delete", Description = "Delete stored procedures", Category = "SP Builder" },
            new { PermissionKey = "StoredProcedures:Execute", Resource = "StoredProcedures", Action = "Execute", Description = "Execute stored procedures", Category = "SP Builder" },

            // System
            new { PermissionKey = "System:ViewLogs", Resource = "System", Action = "ViewLogs", Description = "View system logs", Category = "System" },
            new { PermissionKey = "System:ManageSettings", Resource = "System", Action = "ManageSettings", Description = "Manage system settings", Category = "System" }
        };

        foreach (var perm in permissions)
        {
            await connection.ExecuteAsync(SeedDataQueries.InsertPermissions, perm);
        }

        // 3. Map Permissions to Roles
        // Admin: All permissions
        foreach (var perm in permissions)
        {
            await connection.ExecuteAsync(SeedDataQueries.InsertRolePermissions, new { RoleName = "Admin", perm.PermissionKey });
        }

        var userPermissions = permissions.Where(p => 
            // User: Read + Context Create/Update (excluding Delete/Review)
            p.PermissionKey.EndsWith(":Read") ||
            (p.PermissionKey.StartsWith("Contexts:") && 
             p.Action != "Delete" && 
             p.Action != "Review")
        ).Select(p => p.PermissionKey);

        foreach (var key in userPermissions)
        {
             await connection.ExecuteAsync(SeedDataQueries.InsertRolePermissions, new { RoleName = "User", PermissionKey = key });
        }

        // Manager: All except User/Role Management
        var managerPermissions = permissions.Where(p => 
            p.Category != "User Management" && p.Category != "Role Management"
        ).Select(p => p.PermissionKey);

        foreach (var key in managerPermissions)
        {
            await connection.ExecuteAsync(SeedDataQueries.InsertRolePermissions, new { RoleName = "Manager", PermissionKey = key });
        }

        // ReadOnly: All Read permissions
        var readOnlyPermissions = permissions.Where(p => p.Action == "Read").Select(p => p.PermissionKey);
        foreach (var key in readOnlyPermissions)
        {
            await connection.ExecuteAsync(SeedDataQueries.InsertRolePermissions, new { RoleName = "ReadOnly", PermissionKey = key });
        }

        // 4. Migrate Users
        await connection.ExecuteAsync(SeedDataQueries.MigrateUserRoles);
        _logger.LogInformation("Roles and Permissions seeded successfully.");
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