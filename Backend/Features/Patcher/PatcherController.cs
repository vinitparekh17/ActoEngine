using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Api.Attributes;
using ActoEngine.WebApi.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Features.Patcher;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PatcherController(
    IPatcherService patcherService,
    IPatcherRepository patcherRepo,
    ILogger<PatcherController> log) : ControllerBase
{
    /// <summary>
    /// Check if patches are stale (file modified since last patch generation)
    /// </summary>
    [HttpPost("check-status")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<List<PagePatchStatus>>>> CheckPatchStatus(
        [FromBody] PatchStatusRequest request)
    {
        try
        {
            var statuses = await patcherService.CheckPatchStatusAsync(request);
            var staleCount = statuses.Count(s => s.NeedsRegeneration);

            return Ok(ApiResponse<List<PagePatchStatus>>.Success(
                statuses,
                $"Checked {statuses.Count} pages. {staleCount} need regeneration."));
        }
        catch (InvalidOperationException ex)
        {
            log.LogWarning(ex, "Patch status check failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<List<PagePatchStatus>>.Failure(
                "Patch status check failed", ["An internal error occurred while processing the request."]));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error checking patch status for project {ProjectId}", request.ProjectId);
            return StatusCode(500, ApiResponse<List<PagePatchStatus>>.Failure(
                "Patch status check failed", ["An unexpected error occurred. Please check the logs."]));
        }
    }

    /// <summary>
    /// Generate a patch zip with page files and SQL scripts
    /// </summary>
    [HttpPost("generate")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<PatchGenerationResponse>>> GeneratePatch(
        [FromBody] PatchGenerationRequest request)
    {
        try
        {
            var userId = HttpContext.GetUserId();
            
            var result = await patcherService.GeneratePatchAsync(request, userId);

            return Ok(ApiResponse<PatchGenerationResponse>.Success(
                result,
                $"Patch generated with {result.FilesIncluded.Count} files."));
        }
        catch (InvalidOperationException ex)
        {
            log.LogWarning(ex, "Patch generation failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<PatchGenerationResponse>.Failure(
                "Patch generation failed", ["An internal error occurred while processing the request."]));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error generating patch for project {ProjectId}", request.ProjectId);
            return StatusCode(500, ApiResponse<PatchGenerationResponse>.Failure(
                "Patch generation failed", ["An unexpected error occurred. Please check the logs."]));
        }
    }

    /// <summary>
    /// Download a previously generated patch by ID
    /// </summary>
    [HttpGet("download/{patchId}")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<IActionResult> DownloadPatch(int patchId)
    {
        try
        {
            var patch = await patcherRepo.GetPatchByIdAsync(patchId);
            if (patch == null)
            {
                return NotFound(ApiResponse<object>.Failure(
                    "Patch not found", [$"No patch with ID {patchId} exists."]));
            }

            if (string.IsNullOrEmpty(patch.PatchFilePath) || !System.IO.File.Exists(patch.PatchFilePath))
            {
                return NotFound(ApiResponse<object>.Failure(
                    "Patch file not found", ["The patch file no longer exists on disk."]));
            }

            var fileName = Path.GetFileName(patch.PatchFilePath);
            return PhysicalFile(patch.PatchFilePath, "application/zip", fileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error downloading patch {PatchId}", patchId);
            return StatusCode(500, ApiResponse<object>.Failure(
                "Download failed", ["An unexpected error occurred."]));
        }
    }

    /// <summary>
    /// Get patch generation history for a project
    /// </summary>
    [HttpGet("history/{projectId}")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<List<PatchHistoryRecord>>>> GetPatchHistory(int projectId)
    {
        try
        {
            var history = await patcherRepo.GetPatchHistoryAsync(projectId);

            return Ok(ApiResponse<List<PatchHistoryRecord>>.Success(
                history,
                $"Found {history.Count} patch records."));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error getting patch history for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<PatchHistoryRecord>>.Failure(
                "History fetch failed", ["An unexpected error occurred."]));
        }
    }

    /// <summary>
    /// Get project patch configuration (paths)
    /// </summary>
    [HttpGet("config/{projectId}")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<ProjectPatchConfig>>> GetPatchConfig(int projectId)
    {
        try
        {
            var config = await patcherRepo.GetPatchConfigAsync(projectId);
            if (config == null)
            {
                return NotFound(ApiResponse<ProjectPatchConfig>.Failure(
                    "Config not found", [$"No patch config found for project {projectId}."]));
            }

            return Ok(ApiResponse<ProjectPatchConfig>.Success(config, "Patch configuration retrieved."));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error getting patch config for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<ProjectPatchConfig>.Failure(
                "Config fetch failed", ["An unexpected error occurred."]));
        }
    }

    /// <summary>
    /// Update project patch configuration (paths)
    /// </summary>
    [HttpPost("config/{projectId}")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<string>>> CreateOrUpdatePatchConfig(
        int projectId,
        [FromBody] PatchConfigRequest config)
    {
        try
        {
            await patcherRepo.UpdatePatchConfigAsync(projectId, config);

            return Ok(ApiResponse<string>.Success(
                "Updated",
                "Patch configuration updated successfully."));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error updating patch config (POST) for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<string>.Failure(
                "Config update failed", ["An unexpected error occurred."]));
        }
    }

    /// <summary>
    /// Update project patch configuration (paths)
    /// </summary>
    [HttpPut("config/{projectId}")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<string>>> UpdatePatchConfig(
        int projectId,
        [FromBody] PatchConfigRequest config)
    {
        try
        {
            await patcherRepo.UpdatePatchConfigAsync(projectId, config);

            return Ok(ApiResponse<string>.Success(
                "Updated",
                "Patch configuration updated successfully."));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error updating patch config for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<string>.Failure(
                "Config update failed", ["An unexpected error occurred."]));
        }
    }
}
