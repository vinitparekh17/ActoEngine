// Infrastructure/Data/BaseRepository.cs
using System.Data;
using ActoEngine.WebApi.Services.Database;
using Dapper;

namespace ActoEngine.WebApi.Repositories;

public abstract class BaseRepository(IDbConnectionFactory connectionFactory, ILogger logger)
{
    protected readonly IDbConnectionFactory _connectionFactory = connectionFactory;
    protected readonly ILogger _logger = logger;

    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql, 
        object? parameters = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _logger.LogDebug("Executing scalar query: {Sql}", sql);
            return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scalar query: {Sql}", sql);
            throw;
        }
    }

    protected async Task<IEnumerable<T>> QueryAsync<T>(
        string sql, 
        object? parameters = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _logger.LogDebug("Executing query: {Sql}", sql);
            return await connection.QueryAsync<T>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {Sql}", sql);
            throw;
        }
    }

    protected async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql, 
        object? parameters = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _logger.LogDebug("Executing single query: {Sql}", sql);
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing single query: {Sql}", sql);
            throw;
        }
    }

    protected async Task<int> ExecuteAsync(
        string sql, 
        object? parameters = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _logger.LogDebug("Executing command: {Sql}", sql);
            return await connection.ExecuteAsync(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Sql}", sql);
            throw;
        }
    }

    protected async Task<T> ExecuteInTransactionAsync<T>(
        Func<IDbConnection, IDbTransaction, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await operation(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}