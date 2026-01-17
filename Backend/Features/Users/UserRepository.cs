using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using ActoEngine.WebApi.Shared.Exceptions;

namespace ActoEngine.WebApi.Features.Users;

public interface IUserRepository
{
    Task<(IEnumerable<User> Users, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task UpdatePasswordAsync(int userId, string passwordHash, string? updatedBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task RemoveRoleFromUsersAsync(int roleId, CancellationToken cancellationToken = default);
}

public class UserRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<UserRepository> logger) : BaseRepository(connectionFactory, logger), IUserRepository
{
    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var userDto = await QueryFirstOrDefaultAsync<UserDto>(
            UserSqlQueries.GetById,
            new { UserID = id },
            cancellationToken);

        return userDto?.ToDomain();
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var userDto = await QueryFirstOrDefaultAsync<UserDto>(
            UserSqlQueries.GetByUserName,
            new { Username = userName },
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
            user.Username,
            user.PasswordHash,
            user.FullName,
            user.Role,
            user.CreatedAt,
            user.CreatedBy
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
            user.UserID,
            user.FullName,
            user.Role,
            user.IsActive,
            UpdatedAt = DateTime.UtcNow,
            user.UpdatedBy
        };

        var rowsAffected = await ExecuteAsync(
            UserSqlQueries.Update,
            parameters,
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new NotFoundException($"User with ID {user.UserID} not found");
        }
    }

    public async Task UpdatePasswordAsync(int userId, string passwordHash, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            UserID = userId,
            PasswordHash = passwordHash,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };

        var rowsAffected = await ExecuteAsync(
            UserSqlQueries.UpdatePassword,
            parameters,
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new NotFoundException($"User with ID {userId} not found");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await ExecuteAsync(
            UserSqlQueries.HardDelete,
            new { UserID = id },
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new NotFoundException($"User with ID {id} not found");
        }
    }

    public async Task RemoveRoleFromUsersAsync(int roleId, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            UserSqlQueries.UpdateRoleForUsers,
            new { RoleId = roleId },
            cancellationToken);
    }

    private class UserDto
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public bool IsActive { get; set; }
        public string Role { get; set; } = "User";
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        public User ToDomain()
        {
            return new User(UserID, Username, PasswordHash, FullName, IsActive, Role, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy);
        }
    }
}
