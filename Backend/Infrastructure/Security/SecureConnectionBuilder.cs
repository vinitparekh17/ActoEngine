using System.Security;
using Microsoft.Data.SqlClient;

namespace ActoEngine.WebApi.Infrastructure.Security;

/// <summary>
/// Provides secure methods for building SQL Server connection strings and credentials.
/// Uses SqlConnectionStringBuilder to avoid string concatenation vulnerabilities and
/// SqlCredential for credential separation from connection strings.
/// </summary>
public static class SecureConnectionBuilder
{
    /// <summary>
    /// Builds a connection string using SqlConnectionStringBuilder.
    /// Does NOT include credentials - use SqlCredential separately for security.
    /// </summary>
    /// <param name="server">Server hostname or IP address.</param>
    /// <param name="port">SQL Server port (default 1433).</param>
    /// <param name="database">Database name.</param>
    /// <param name="encrypt">Whether to encrypt the connection (default: true).</param>
    /// <param name="trustServerCertificate">Whether to trust the server certificate (default: false).</param>
    /// <param name="connectionTimeout">Connection timeout in seconds (default: 30).</param>
    /// <param name="applicationName">Application name for tracking (optional).</param>
    /// <param name="environment">Optional host environment for environment-specific defaults.</param>
    /// <returns>A SqlConnectionStringBuilder with configured connection properties.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
    public static SqlConnectionStringBuilder BuildConnectionString(
        string server,
        int port,
        string database,
        bool encrypt = true,
        bool trustServerCertificate = false,
        int connectionTimeout = 30,
        string? applicationName = null,
        IHostEnvironment? environment = null)
    {
        if (string.IsNullOrWhiteSpace(server))
            throw new ArgumentException("Server cannot be null or empty.", nameof(server));
        if (string.IsNullOrWhiteSpace(database))
            throw new ArgumentException("Database cannot be null or empty.", nameof(database));
        if (port <= 0 || port > 65535)
            throw new ArgumentException("Port must be between 1 and 65535.", nameof(port));

        // Use user-provided trustServerCertificate, but in development mode default to true if not explicitly set
        var effectiveTrustCert = trustServerCertificate || (environment?.IsDevelopment() ?? false);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = port == 1433 ? server : $"{server},{port}",
            InitialCatalog = database,
            IntegratedSecurity = false,
            Encrypt = encrypt,
            TrustServerCertificate = effectiveTrustCert,
            // Connection pooling settings for credential management
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 100,
            ConnectTimeout = Math.Clamp(connectionTimeout, 5, 120),
            // Prevent connection string from being read after creation (security feature)
            PersistSecurityInfo = false,
            ApplicationName = applicationName ?? "ActoEngine"
        };

        return builder;
    }

    /// <summary>
    /// Creates a SqlCredential from username and password.
    /// Uses SecureString for the password to minimize plaintext exposure in memory.
    /// </summary>
    /// <param name="username">SQL Server username.</param>
    /// <param name="password">SQL Server password.</param>
    /// <returns>A SqlCredential instance.</returns>
    /// <exception cref="ArgumentException">Thrown when username or password is null or empty.</exception>
    /// <remarks>
    /// Note: The password is received as a plain string from HTTP deserialization.
    /// This method converts it to SecureString to minimize exposure, but complete
    /// memory protection would require encrypted payloads at the infrastructure level.
    /// </remarks>
    public static SqlCredential CreateCredential(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        var securePassword = CreateSecureString(password);
        securePassword.MakeReadOnly();

        return new SqlCredential(username, securePassword);
    }

    /// <summary>
    /// Creates a SecureString from a plain string.
    /// Should be used immediately after receiving the password and the original
    /// string should be discarded as soon as possible.
    /// </summary>
    /// <param name="input">The plain string to convert.</param>
    /// <returns>A SecureString containing the input characters.</returns>
    public static SecureString CreateSecureString(string input)
    {
        var secure = new SecureString();
        if (!string.IsNullOrEmpty(input))
        {
            foreach (char c in input)
            {
                secure.AppendChar(c);
            }
        }
        return secure;
    }

    /// <summary>
    /// Builds a connection string with integrated security (Windows Authentication).
    /// For use with service accounts where SQL authentication is not required.
    /// </summary>
    /// <param name="server">Server hostname or IP address.</param>
    /// <param name="port">SQL Server port (default 1433).</param>
    /// <param name="database">Database name.</param>
    /// <param name="environment">Optional host environment for production-specific settings.</param>
    /// <returns>A SqlConnectionStringBuilder configured for Windows Authentication.</returns>
    public static SqlConnectionStringBuilder BuildIntegratedSecurityConnectionString(
        string server,
        int port,
        string database,
        IHostEnvironment? environment = null)
    {
        var builder = BuildConnectionString(server, port, database, environment: environment);
        builder.IntegratedSecurity = true;
        return builder;
    }
}
