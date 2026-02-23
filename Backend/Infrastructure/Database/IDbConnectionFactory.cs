using Microsoft.Data.SqlClient;
using System.Data;

namespace ActoEngine.WebApi.Infrastructure.Database;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
    IDbConnection CreateConnection();
    Task<IDbConnection> CreateConnectionWithConnectionString(string connectionString, CancellationToken cancellationToken = default);
}

public class SqlServerConnectionFactory(
    IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not found");

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
    public async Task<IDbConnection> CreateConnectionWithConnectionString(string connectionString, CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
