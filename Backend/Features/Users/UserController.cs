using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Attributes;
using ActoEngine.WebApi.Extensions;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Models.Requests.Users;
using ActoEngine.WebApi.Models.Responses.Users;
using ActoEngine.WebApi.Services.UserManagementService;
using ActoEngine.WebApi.Services.Auth;
using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserManagementController(IUserManagementService userManagementService, IAuthService authService) : ControllerBase
{
    private readonly IUserManagementService _userManagementService = userManagementService;
    private readonly IAuthService _authService = authService;

    [HttpGet]
    [RequirePermission("Users:Read")]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (users, totalCount) = await _userManagementService.GetAllUsersAsync(page, pageSize);
        return Ok(ApiResponse<object>.Success(new { users, totalCount }, "Users retrieved successfully"));
    }

    [HttpGet("{userId}")]
    [RequirePermission("Users:Read")]
    public async Task<IActionResult> GetUser(int userId)
    {
        var user = await _userManagementService.GetUserWithPermissionsAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Failure("User not found"));
        }

        return Ok(ApiResponse<UserWithPermissionsDto>.Success(user, "User retrieved successfully"));
    }

    [HttpPost]
    [RequirePermission("Users:Create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Failure("Invalid request data",
                [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
        }

        var createdBy = HttpContext.GetUserId();
        if (createdBy == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
        }

        try
        {
            var user = await _userManagementService.CreateUserAsync(request, createdBy.Value);
            return Ok(ApiResponse<UserDto>.Success(user, "User created successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }

    [HttpPut("{userId}")]
    [RequirePermission("Users:Update")]
    public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserRequest request)
    {
        if (userId != request.UserId)
        {
            return BadRequest(ApiResponse<object>.Failure("User ID mismatch"));
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Failure("Invalid request data",
                [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
        }

        var updatedBy = HttpContext.GetUserId();
        if (updatedBy == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
        }

        try
        {
            await _userManagementService.UpdateUserAsync(request, updatedBy.Value);
            return Ok(ApiResponse<object>.Success(new { }, "User updated successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }

    [HttpPatch("{userId}/status")]
    [RequirePermission("Users:Activate")]
    public async Task<IActionResult> ToggleUserStatus(int userId, [FromBody] ToggleStatusRequest request)
    {
        var updatedBy = HttpContext.GetUserId();
        if (updatedBy == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
        }

        try
        {
            await _userManagementService.ToggleUserStatusAsync(userId, request.IsActive, updatedBy.Value);
            return Ok(ApiResponse<object>.Success(new { }, $"User {(request.IsActive ? "activated" : "deactivated")} successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }

    [HttpDelete("{userId}")]
    [RequirePermission("Users:Delete")]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        var currentUserId = HttpContext.GetUserId();
        if (currentUserId == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
        }

        if (currentUserId == userId)
        {
            return BadRequest(ApiResponse<object>.Failure("Cannot delete the currently authenticated user."));
        }

        try
        {
            await _userManagementService.DeleteUserAsync(userId);
            return Ok(ApiResponse<object>.Success(new { }, "User deleted successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }

    [HttpPost("{userId}/change-password")]
    [RequirePermission("Users:Update")]
    public async Task<IActionResult> ChangePassword(int userId, [FromBody] ChangePasswordRequestDto request)
    {
        var updatedBy = HttpContext.GetUserId();
        if (updatedBy == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
        }

        try
        {
            var changePasswordRequest = new ChangePasswordRequest
            {
                UserId = userId,
                NewPassword = request.NewPassword
            };
            await _userManagementService.ChangePasswordAsync(changePasswordRequest, updatedBy.Value);

            // SECURITY FIX: Invalidate all sessions for this user after password change
            try
            {
                await _authService.LogoutByUserIdAsync(userId);
                return Ok(ApiResponse<object>.Success(new { }, "Password changed successfully. All sessions have been logged out."));
            }
            catch (Exception logoutEx)
            {
                // Log the error but don't fail the request - password was changed successfully
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<UserManagementController>>();
                logger.LogError(logoutEx, "Failed to logout user {UserId} after password change", userId);
                return Ok(ApiResponse<object>.Success(new { }, "Password changed successfully, but some sessions may remain active. Please log in again."));
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }
}
