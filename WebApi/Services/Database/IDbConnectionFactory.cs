using System.Data;
using Microsoft.Data.SqlClient;

namespace ActoEngine.WebApi.Services.Database;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
    IDbConnection CreateConnection();
    IDbConnection CreateConnectionWithConnectionString(string connectionString);
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
    public IDbConnection CreateConnectionWithConnectionString(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}