using ActoEngine.WebApi.Features.Auth;
using ActoEngine.WebApi.Features.Users.Dtos.Requests;
using ActoEngine.WebApi.Features.Users.Dtos.Responses;
using ActoEngine.WebApi.Features.Projects;
using Dapper;
using ActoEngine.WebApi.Shared.Validation;

namespace ActoEngine.WebApi.Features.Users;

public interface IUserManagementService
{
    Task<(IEnumerable<UserDto> Users, int TotalCount)> GetAllUsersAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<UserWithRoleDto?> GetUserWithRoleAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, int createdBy, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(UpdateUserRequest request, int updatedBy, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, int updatedBy, CancellationToken cancellationToken = default);
    Task ToggleUserStatusAsync(int userId, bool isActive, int updatedBy, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(int userId, CancellationToken cancellationToken = default);
}

public class UserManagementService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IPasswordValidator passwordValidator,
    Infrastructure.Database.IDbConnectionFactory connectionFactory,
    IProjectRepository projectRepository,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    public async Task<(IEnumerable<UserDto> Users, int TotalCount)> GetAllUsersAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var offset = (page - 1) * pageSize;
        var users = await connection.QueryAsync<UserDto>(
            UserSqlQueries.GetAllWithRoles,
            new { Offset = offset, Limit = pageSize });

        var totalCount = await connection.ExecuteScalarAsync<int>(UserSqlQueries.GetCount);

        return (users, totalCount);
    }

    public async Task<UserWithRoleDto?> GetUserWithRoleAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var user = await connection.QueryFirstOrDefaultAsync<UserDto>(
            UserSqlQueries.GetUserWithRole,
            new { UserId = userId });

        if (user == null)
        {
            return null;
        }

        // Note: Permissions are managed through roles, so we only return the user and role information
        // The role name is already included in the UserDto from the query
        // RoleName can be null if user has no assigned role (LEFT JOIN result)

        return new UserWithRoleDto
        {
            User = user,
            RoleName = user.RoleName ?? "Unassigned"
        };
    }

    public async Task<UserDto> CreateUserAsync(
        CreateUserRequest request,
        int createdBy,
        CancellationToken cancellationToken = default)
    {
        // Check for duplicate username
        var existing = await userRepository.GetByUserNameAsync(request.Username, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Username '{request.Username}' already exists");
        }

        // Validate password strength
        var (isValid, errorMessage) = passwordValidator.ValidatePassword(request.Password);
        if (!isValid)
        {
            throw new InvalidOperationException(errorMessage ?? "Invalid password");
        }

        var passwordHash = passwordHasher.HashPassword(request.Password);

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

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
            }) ?? throw new InvalidOperationException("Failed to create user");
        logger.LogInformation("Created user {Username} (ID: {UserId})", request.Username, user.UserId);
        return user;
    }

    public async Task UpdateUserAsync(
        UpdateUserRequest request,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken) ?? throw new InvalidOperationException($"User {request.UserId} not found");
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            UserSqlQueries.UpdateUserManagement,
            new
            {
                request.UserId,
                request.FullName,
                request.RoleId,
                request.IsActive,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            });

        logger.LogInformation("Updated user {Username} (ID: {UserId})", user.Username, request.UserId);
    }

    public async Task ChangePasswordAsync(
        ChangePasswordRequest request,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken) ?? throw new InvalidOperationException($"User {request.UserId} not found");

        // Validate password strength
        var (isValid, errorMessage) = passwordValidator.ValidatePassword(request.NewPassword);
        if (!isValid)
        {
            throw new InvalidOperationException(errorMessage ?? "Invalid password");
        }

        var newPasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.ChangePassword(newPasswordHash, updatedBy.ToString());

        await userRepository.UpdatePasswordAsync(user.UserID, user.PasswordHash, updatedBy.ToString(), cancellationToken);
        logger.LogInformation("Changed password for user {Username} (ID: {UserId})", user.Username, request.UserId);
    }

    public async Task ToggleUserStatusAsync(
        int userId,
        bool isActive,
        int updatedBy,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken) ?? throw new InvalidOperationException($"User {userId} not found");
        user.SetActiveStatus(isActive, updatedBy.ToString());
        await userRepository.UpdateAsync(user, cancellationToken);

        logger.LogInformation(
            "{Action} user {Username} (ID: {UserId})",
            isActive ? "Activated" : "Deactivated",
            user.Username,
            userId);
    }

    public async Task DeleteUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken) ?? throw new InvalidOperationException($"User {userId} not found");

        // Auto-cleanup: Remove all project memberships before deleting user
        // This prevents FK_ProjectMembers_Users constraint violations (ON DELETE NO ACTION)
        var projectMemberships = await projectRepository.GetUserProjectMembershipsAsync(userId, cancellationToken);
        var membershipsList = projectMemberships.ToList();

        if (membershipsList.Any())
        {
            logger.LogInformation("Removing {Count} project membership(s) for user {UserId} before deletion", membershipsList.Count, userId);

            foreach (var projectId in membershipsList)
            {
                await projectRepository.RemoveProjectMemberAsync(projectId, userId, cancellationToken);
            }
        }

        await userRepository.DeleteAsync(userId, cancellationToken);
        logger.LogInformation("Deleted user {Username} (ID: {UserId})", user.Username, userId);
    }
}
