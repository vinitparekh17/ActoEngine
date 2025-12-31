using Microsoft.Data.SqlClient;

namespace ActoEngine.WebApi.Extensions
{
    public static class DatabaseExtensions
    {
        public static void AddDatabaseConfiguration(this WebApplicationBuilder builder)
        {
            var dbServer = Environment.GetEnvironmentVariable("DB_SERVER") ?? builder.Configuration["DB_SERVER"] ?? "127.0.0.1";
            var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? builder.Configuration["DB_PORT"] ?? "1433";
            var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? builder.Configuration["DB_NAME"] ?? "ActoEngine";
            var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? builder.Configuration["DB_USER"] ?? "sa";
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? builder.Configuration["DB_PASSWORD"]
                ?? throw new InvalidOperationException("Database password must be set via DB_PASSWORD environment variable");

            // Build secure connection string with conditional certificate validation
            // Only trust server certificate if explicitly enabled via environment variable
            var trustCert = string.Equals(Environment.GetEnvironmentVariable("DB_TRUST_CERT")?.ToLower(), "true");

            var connStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = $"{dbServer},{dbPort}",
                InitialCatalog = dbName,
                UserID = dbUser,
                Password = dbPassword,
                MultipleActiveResultSets = true,
                Encrypt = true, // Always enable encryption
                TrustServerCertificate = trustCert // Only trust untrusted certs in development
            };

            builder.Configuration["ConnectionStrings:DefaultConnection"] = connStringBuilder.ConnectionString;

            // Set admin password from environment variables
            var adminPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") ?? builder.Configuration["SEED_ADMIN_PASSWORD"]
                ?? throw new InvalidOperationException("Admin password must be set via SEED_ADMIN_PASSWORD environment variable");
            builder.Configuration["DatabaseSeeding:DefaultPasswords:AdminUser"] = adminPassword;
        }
    }
}
