using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace ActoX.Infrastructure.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
    IDbConnection CreateConnection();
}

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerConnectionFactory> _logger;

    public SqlServerConnectionFactory(
        IConfiguration configuration, 
        ILogger<SqlServerConnectionFactory> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection not found");
        _logger = logger;
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public IDbConnection CreateConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}