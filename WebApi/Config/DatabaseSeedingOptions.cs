namespace ActoEngine.WebApi.Config;

public class DatabaseSeedingOptions
{
    public bool Enabled { get; set; } = true;
    public int SeedingTimeoutSeconds { get; set; } = 60;
    public DefaultPasswordsOptions DefaultPasswords { get; set; } = new();
    public AdminUserOptions AdminUser { get; set; } = new();
}

public class DefaultPasswordsOptions
{
    public string AdminUser { get; set; } = "AdminSecure123!@#";
}

public class AdminUserOptions
{
    public string Username { get; set; } = "admin";
    public string FullName { get; set; } = "System Administrator";
    public string Role { get; set; } = "Admin";
}