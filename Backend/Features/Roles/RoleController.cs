using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Api.Attributes;
using ActoEngine.WebApi.Features.Roles.Dtos;
using ActoEngine.WebApi.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Features.Roles;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RoleController(IRoleService roleService) : ControllerBase
{
    private readonly IRoleService _roleService = roleService;

    [HttpGet]
    [RequirePermission("Roles:Read")]
    public async Task<IActionResult> GetAllRoles()
    {
        var roles = await _roleService.GetAllRolesAsync();
        return Ok(ApiResponse<IEnumerable<Role>>.Success(roles, "Roles retrieved successfully"));
    }

    [HttpGet("{roleId}")]
    [RequirePermission("Roles:Read")]
    public async Task<IActionResult> GetRole(int roleId)
    {
        var role = await _roleService.GetRoleWithPermissionsAsync(roleId);
        if (role == null)
        {
            return NotFound(ApiResponse<object>.Failure("Role not found"));
        }

        return Ok(ApiResponse<RoleWithPermissions>.Success(role, "Role retrieved successfully"));
    }

    [HttpPost]
    [RequirePermission("Roles:Create")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Failure("Invalid request data",
                [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
        }

        var userId = HttpContext.GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
        }

        try
        {
            var role = await _roleService.CreateRoleAsync(request, userId.Value);
            return Ok(ApiResponse<Role>.Success(role, "Role created successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }

    [HttpPut("{roleId}")]
    [RequirePermission("Roles:Update")]
    public async Task<IActionResult> UpdateRole(int roleId, [FromBody] UpdateRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Failure("Invalid request data",
                [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
        }

        var userId = HttpContext.GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
        }


        try
        {
            // Use roleId from URL parameter
            await _roleService.UpdateRoleAsync(roleId, request, userId.Value);
            return Ok(ApiResponse<object>.Success(new { }, "Role updated successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }

    [HttpDelete("{roleId}")]
    [RequirePermission("Roles:Delete")]
    public async Task<IActionResult> DeleteRole(int roleId)
    {
        try
        {
            await _roleService.DeleteRoleAsync(roleId);
            return Ok(ApiResponse<object>.Success(new { }, "Role deleted successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
    }
}
