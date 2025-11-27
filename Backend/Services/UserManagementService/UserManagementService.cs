using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Auth;
using ActoEngine.WebApi.SqlQueries;
using Dapper;

namespace ActoEngine.WebApi.Services.UserManagementService;

public interface IUserManagementService
{
    Task<(IEnumerable<UserDto> Users, int TotalCount)> GetAllUsersAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<UserWithPermissionsDto?> GetUserWithPermissionsAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, int createdBy, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(UpdateUserRequest request, int updatedBy, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, int updatedBy, CancellationToken cancellationToken = default);
    Task ToggleUserStatusAsync(int userId, bool isActive, int updatedBy, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(int userId, CancellationToken cancellationToken = default);
}

public class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _userRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly Services.Database.IDbConnectionFactory _connectionFactory;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        IUserRepository userRepository,
        IPermissionRepository permissionRepository,
        IPasswordHasher passwordHasher,
        Services.Database.IDbConnectionFactory connectionFactory,
        ILogger<UserManagementService> logger)
    {
        _userRepository = userRepository;
        _permissionRepository = permissionRepository;
        _passwordHasher = passwordHasher;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<(IEnumerable<UserDto> Users, int TotalCount)> GetAllUsersAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var offset = (page - 1) * pageSize;
        var users = await connection.QueryAsync<UserDto>(
            UserSqlQueries.GetAllWithRoles,
            new { Offset = offset, Limit = pageSize });

        var totalCount = await connection.ExecuteScalarAsync<int>(UserSqlQueries.GetCount);

        return (users, totalCount);
    }

    public async Task<UserWithPermissionsDto?> GetUserWithPermissionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var user = await connection.QueryFirstOrDefaultAsync<UserDto>(
            UserSqlQueries.GetUserWithRole,
            new { UserId = userId });

        if (user == null) return null;

        var permissions = await _permissionRepository.GetUserPermissionsAsync(userId, cancellationToken);

        return new UserWithPermissionsDto
        {
            User = user,
            Permissions = permissions.ToList()
        };
    }

    public async Task<UserDto> CreateUserAsync(
        CreateUserRequest request,
        int createdBy,
        CancellationToken cancellationToken = default)
    {
        // Check for duplicate username
        var existing = await _userRepository.GetByUserNameAsync(request.Username, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Username '{request.Username}' already exists");

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var user = await connection.QueryFirstOrDefaultAsync<UserDto>(
            UserSqlQueries.InsertWithRole,
            new
            {
                request.Username,
                PasswordHash = passwordHash,
                request.FullName,
                request.RoleId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            });

        if (user == null)
            throw new InvalidOperationException("Failed to create user");

        _logger.LogInformation("Created user {Username} (ID: {UserId})", request.Username, user.UserId);
        return user;
    }

    public async Task UpdateUserAsync(
        UpdateUserRequest request,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User {request.UserId} not found");

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            UserSqlQueries.UpdateUserManagement,
            new
            {
                UserId = request.UserId,
                request.FullName,
                request.RoleId,
                request.IsActive,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            });

        _logger.LogInformation("Updated user {Username} (ID: {UserId})", user.Username, request.UserId);
    }

    public async Task ChangePasswordAsync(
        ChangePasswordRequest request,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User {request.UserId} not found");

        var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.ChangePassword(newPasswordHash, updatedBy.ToString());

        await _userRepository.UpdateAsync(user, cancellationToken);
        _logger.LogInformation("Changed password for user {Username} (ID: {UserId})", user.Username, request.UserId);
    }

    public async Task ToggleUserStatusAsync(
        int userId,
        bool isActive,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        user.SetActiveStatus(isActive, updatedBy.ToString());
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation(
            "{Action} user {Username} (ID: {UserId})",
            isActive ? "Activated" : "Deactivated",
            user.Username,
            userId);
    }

    public async Task DeleteUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        await _userRepository.DeleteAsync(userId, cancellationToken);
        _logger.LogInformation("Deleted user {Username} (ID: {UserId})", user.Username, userId);
    }
}
