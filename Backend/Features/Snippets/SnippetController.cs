using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Api.Attributes;
using ActoEngine.WebApi.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Features.Snippets;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SnippetController(ISnippetService snippetService) : ControllerBase
{
    private readonly ISnippetService _snippetService = snippetService;

    [HttpGet]
    [RequirePermission("Snippets:Read")]
    public async Task<IActionResult> GetSnippets(
        [FromQuery] string? search,
        [FromQuery] string? language,
        [FromQuery] string? tag,
        [FromQuery] string sortBy = "recent",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        var listParams = new SnippetListParams
        {
            Search = search,
            Language = language,
            Tag = tag,
            SortBy = sortBy,
            Page = Math.Max(page, 1),
            PageSize = Math.Clamp(pageSize, 1, 50)
        };

        var result = await _snippetService.GetSnippetsAsync(listParams, userId.Value);
        return Ok(ApiResponse<PaginatedResult<SnippetListResponse>>.Success(result, "Snippets retrieved successfully"));
    }

    [HttpGet("{snippetId}")]
    [RequirePermission("Snippets:Read")]
    public async Task<IActionResult> GetSnippet(int snippetId)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        var snippet = await _snippetService.GetSnippetByIdAsync(snippetId, userId.Value);
        if (snippet == null)
            return NotFound(ApiResponse<object>.Failure("Snippet not found"));

        return Ok(ApiResponse<SnippetDetailResponse>.Success(snippet, "Snippet retrieved successfully"));
    }

    [HttpGet("filters")]
    [RequirePermission("Snippets:Read")]
    public async Task<IActionResult> GetFilterOptions()
    {
        var options = await _snippetService.GetFilterOptionsAsync();
        return Ok(ApiResponse<SnippetFilterOptions>.Success(options, "Filter options retrieved successfully"));
    }

    [HttpPost]
    [RequirePermission("Snippets:Create")]
    public async Task<IActionResult> CreateSnippet([FromBody] CreateSnippetRequest request)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        var created = await _snippetService.CreateSnippetAsync(request, userId.Value);
        return CreatedAtAction(nameof(GetSnippet), new { snippetId = created.SnippetId },
            ApiResponse<SnippetDetailResponse>.Success(created, "Snippet created successfully"));
    }

    [HttpPut("{snippetId}")]
    [RequirePermission("Snippets:Update")]
    public async Task<IActionResult> UpdateSnippet(int snippetId, [FromBody] UpdateSnippetRequest request)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        var isAdmin = HttpContext.User.FindFirst("role")?.Value == "Admin";
        var success = await _snippetService.UpdateSnippetAsync(snippetId, request, userId.Value, isAdmin);
        if (!success)
            return NotFound(ApiResponse<object>.Failure("Snippet not found or you don't have permission to edit it"));

        return Ok(ApiResponse<object>.Success(new { }, "Snippet updated successfully"));
    }

    [HttpDelete("{snippetId}")]
    [RequirePermission("Snippets:Delete")]
    public async Task<IActionResult> DeleteSnippet(int snippetId)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        var isAdmin = HttpContext.User.FindFirst("role")?.Value == "Admin";
        var success = await _snippetService.DeleteSnippetAsync(snippetId, userId.Value, isAdmin);
        if (!success)
            return NotFound(ApiResponse<object>.Failure("Snippet not found or you don't have permission to delete it"));

        return Ok(ApiResponse<object>.Success(new { }, "Snippet deleted successfully"));
    }

    [HttpPost("{snippetId}/copy")]
    [RequirePermission("Snippets:Read")]
    public async Task<IActionResult> RecordCopy(int snippetId)
    {
        var updated = await _snippetService.IncrementCopyCountAsync(snippetId);
        if (!updated)
            return NotFound(ApiResponse<object>.Failure("Snippet not found"));

        return Ok(ApiResponse<object>.Success(new { }, "Copy recorded"));
    }

    [HttpPost("{snippetId}/favorite")]
    [RequirePermission("Snippets:Read")]
    public async Task<IActionResult> ToggleFavorite(int snippetId)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

        var isFavorited = await _snippetService.ToggleFavoriteAsync(snippetId, userId.Value);
        return Ok(ApiResponse<object>.Success(
            new { isFavorited },
            isFavorited ? "Snippet favorited" : "Snippet unfavorited"));
    }
}
