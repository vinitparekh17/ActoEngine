using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Features.Roles;
using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Api.Attributes;

namespace ActoEngine.WebApi.Features.Permissions;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PermissionController(IPermissionService permissionService) : ControllerBase
{
    private readonly IPermissionService _permissionService = permissionService;

    [HttpGet]
    [RequirePermission("Roles:Read")]
    public async Task<IActionResult> GetAllPermissions()
    {
        var permissions = await _permissionService.GetAllPermissionsAsync();
        return Ok(ApiResponse<IEnumerable<Permission>>.Success(permissions, "Permissions retrieved successfully"));
    }

    [HttpGet("grouped")]
    [RequirePermission("Roles:Read")]
    public async Task<IActionResult> GetPermissionsGrouped()
    {
        var permissionGroups = await _permissionService.GetPermissionsGroupedAsync();
        return Ok(ApiResponse<IEnumerable<PermissionGroupDto>>.Success(permissionGroups, "Permissions retrieved successfully"));
    }
}
