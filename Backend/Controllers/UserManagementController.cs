using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Attributes;
using ActoEngine.WebApi.Extensions;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.UserManagementService;

namespace ActoEngine.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserManagementController(IUserManagementService userManagementService) : ControllerBase
{
    private readonly IUserManagementService _userManagementService = userManagementService;

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
            return NotFound(ApiResponse<object>.Failure("User not found"));

        return Ok(ApiResponse<UserWithPermissionsDto>.Success(user, "User retrieved successfully"));
    }

    [HttpPost]
    [RequirePermission("Users:Create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Failure("Invalid request data",
                [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

        var createdBy = HttpContext.GetUserId();
        if (createdBy == null)
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

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
            return BadRequest(ApiResponse<object>.Failure("User ID mismatch"));

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Failure("Invalid request data",
                [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

        var updatedBy = HttpContext.GetUserId();
        if (updatedBy == null)
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        try
        {
            await _userManagementService.UpdateUserAsync(request, updatedBy.Value);
            return Ok(ApiResponse<object>.Success(null, "User updated successfully"));
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
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        try
        {
            await _userManagementService.ToggleUserStatusAsync(userId, request.IsActive, updatedBy.Value);
            return Ok(ApiResponse<object>.Success(null, $"User {(request.IsActive ? "activated" : "deactivated")} successfully"));
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
        try
        {
            await _userManagementService.DeleteUserAsync(userId);
            return Ok(ApiResponse<object>.Success(null, "User deleted successfully"));
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
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        try
        {
            var changePasswordRequest = new ChangePasswordRequest
            {
                UserId = userId,
                NewPassword = request.NewPassword
            };
            await _userManagementService.ChangePasswordAsync(changePasswordRequest, updatedBy.Value);
            return Ok(ApiResponse<object>.Success(null, "Password changed successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }
}

// Simple request DTO for toggle status
public class ToggleStatusRequest
{
    public bool IsActive { get; set; }
}

// Simple request DTO for change password
public class ChangePasswordRequestDto
{
    public string NewPassword { get; set; } = default!;
}
