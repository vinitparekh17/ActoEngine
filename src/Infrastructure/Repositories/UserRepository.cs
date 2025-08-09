// Infrastructure/Repositories/UserRepository.cs
using Microsoft.Extensions.Logging;

public class UserRepository : BaseRepository, IUserRepository
{
    public UserRepository(
        IDbConnectionFactory connectionFactory, 
        ILogger<UserRepository> logger) 
        : base(connectionFactory, logger)
    {
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userDto = await QueryFirstOrDefaultAsync<UserDto>(
            UserSqlQueries.GetById,
            new { Id = id },
            cancellationToken);

        return userDto?.ToDomain();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var userDto = await QueryFirstOrDefaultAsync<UserDto>(
            UserSqlQueries.GetByEmail,
            new { Email = email },
            cancellationToken);

        return userDto?.ToDomain();
    }

    public async Task<(IEnumerable<User> Users, int TotalCount)> GetAllAsync(
        int page = 1, 
        int pageSize = 10, 
        CancellationToken cancellationToken = default)
    {
        var offset = (page - 1) * pageSize;

        var usersTask = QueryAsync<UserDto>(
            UserSqlQueries.GetAll,
            new { Limit = pageSize, Offset = offset },
            cancellationToken);

        var countTask = ExecuteScalarAsync<int>(
            UserSqlQueries.GetCount,
            cancellationToken: cancellationToken);

        await Task.WhenAll(usersTask, countTask);

        var users = (await usersTask).Select(dto => dto.ToDomain());
        var totalCount = await countTask;

        return (users, totalCount);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt
        };

        var userDto = await QueryFirstOrDefaultAsync<UserDto>(
            UserSqlQueries.Insert,
            parameters,
            cancellationToken);

        return userDto?.ToDomain() ?? throw new InvalidOperationException("Failed to insert user");
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UpdatedAt = DateTime.UtcNow
        };

        var rowsAffected = await ExecuteAsync(
            UserSqlQueries.Update,
            parameters,
            cancellationToken);

        if (rowsAffected == 0)
            throw new NotFoundException($"User with ID {user.Id} not found");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await ExecuteAsync(
            UserSqlQueries.SoftDelete,
            new { Id = id, DeletedAt = DateTime.UtcNow },
            cancellationToken);

        if (rowsAffected == 0)
            throw new NotFoundException($"User with ID {id} not found");
    }

    public Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    // Data Transfer Object for Database Mapping
    private class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string First_Name { get; set; } = string.Empty;
        public string Last_Name { get; set; } = string.Empty;
        public DateTime Created_At { get; set; }
        public DateTime? Updated_At { get; set; }

        public User ToDomain()
        {
            return new User(Id, Email, First_Name, Last_Name, Created_At, Updated_At);
        }
    }
}