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
        [FromBody] PatchStatusRequest request,
        CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<List<PagePatchStatus>>.Failure(
                "Patch status check failed", ["Request body is required."]));
        }

        try
        {
            var statuses = await patcherService.CheckPatchStatusAsync(request, ct);
            var staleCount = statuses.Count(s => s.NeedsRegeneration);

            return Ok(ApiResponse<List<PagePatchStatus>>.Success(
                statuses,
                $"Checked {statuses.Count} pages. {staleCount} need regeneration."));
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Patch status check cancelled for project {ProjectId}", request.ProjectId);
            return StatusCode(499);
        }
        catch (InvalidOperationException ex)
        {
            log.LogWarning(ex, "Patch status check failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<List<PagePatchStatus>>.Failure(
                "Patch status check failed", [ex.Message]));
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
        [FromBody] PatchGenerationRequest request,
        CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<PatchGenerationResponse>.Failure(
                "Patch generation failed", ["Request body is required."]));
        }

        try
        {
            var userId = HttpContext.GetUserId();

            var result = await patcherService.GeneratePatchAsync(request, userId, ct);

            return Ok(ApiResponse<PatchGenerationResponse>.Success(
                result,
                $"Patch generated with {result.FilesIncluded.Count} files."));
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Patch generation cancelled for project {ProjectId}", request.ProjectId);
            return StatusCode(499);
        }
        catch (InvalidOperationException ex)
        {
            log.LogWarning(ex, "Patch generation failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<PatchGenerationResponse>.Failure(
                "Patch generation failed", [ex.Message]));
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
    public async Task<IActionResult> DownloadPatch(int patchId, CancellationToken ct)
    {
        try
        {
            var resolvedPatch = await ResolvePatchDownloadAsync(patchId, ct);
            if (resolvedPatch.ErrorResult != null)
            {
                return resolvedPatch.ErrorResult;
            }

            return PhysicalFile(
                resolvedPatch.FilePath!,
                "application/zip",
                resolvedPatch.DownloadFileName!,
                enableRangeProcessing: true);
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Patch download cancelled for patch {PatchId}", patchId);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error downloading patch {PatchId}", patchId);
            return StatusCode(500, ApiResponse<object>.Failure(
                "Download failed", ["An unexpected error occurred."]));
        }
    }

    [HttpGet("download-script/{patchId}")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<IActionResult> DownloadPatchScript(int patchId, CancellationToken ct)
    {
        try
        {
            var resolvedPatch = await ResolvePatchDownloadAsync(patchId, ct);
            if (resolvedPatch.ErrorResult != null)
            {
                return resolvedPatch.ErrorResult;
            }

            var scriptContent = patcherService.BuildPatchScript(resolvedPatch.FilePath!);
            var scriptFileName = Path.GetFileNameWithoutExtension(resolvedPatch.DownloadFileName!) + ".ps1";
            var scriptBytes = System.Text.Encoding.UTF8.GetBytes(scriptContent);
            return File(scriptBytes, "text/plain; charset=utf-8", scriptFileName);
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Patch script download cancelled for patch {PatchId}", patchId);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error downloading patch script {PatchId}", patchId);
            return StatusCode(500, ApiResponse<object>.Failure(
                "Download failed", ["An unexpected error occurred."]));
        }
    }

    /// <summary>
    /// Get patch generation history for a project
    /// </summary>
    [HttpGet("history/{projectId}")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<List<PatchHistoryRecord>>>> GetPatchHistory(
        int projectId,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        try
        {
            var history = await patcherRepo.GetPatchHistoryAsync(projectId, offset, limit, ct);

            return Ok(ApiResponse<List<PatchHistoryRecord>>.Success(
                history,
                $"Found {history.Count} patch records."));
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Patch history fetch cancelled for project {ProjectId}", projectId);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error getting patch history for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<List<PatchHistoryRecord>>.Failure(
                "History fetch failed", ["An unexpected error occurred."]));
        }
    }

    [HttpGet("history/{projectId}/paged")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<PatchHistoryPageResponse>>> GetPatchHistoryPaged(
        int projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var paged = await patcherRepo.GetPatchHistoryPagedAsync(projectId, page, pageSize, ct);
            return Ok(ApiResponse<PatchHistoryPageResponse>.Success(
                paged,
                $"Found {paged.Items.Count} patch records (page {paged.Page})."));
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Paged patch history fetch cancelled for project {ProjectId}", projectId);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error getting patch history for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<PatchHistoryPageResponse>.Failure(
                "History fetch failed", ["An unexpected error occurred."]));
        }
    }

    /// <summary>
    /// Get project patch configuration (paths)
    /// </summary>
    [HttpGet("config/{projectId}")]
    [RequirePermission("StoredProcedures:Create")]
    public async Task<ActionResult<ApiResponse<ProjectPatchConfig>>> GetPatchConfig(int projectId, CancellationToken ct)
    {
        try
        {
            var config = await patcherRepo.GetPatchConfigAsync(projectId, ct);
            if (config == null)
            {
                return NotFound(ApiResponse<ProjectPatchConfig>.Failure(
                    "Config not found", [$"No patch config found for project {projectId}."]));
            }

            return Ok(ApiResponse<ProjectPatchConfig>.Success(config, "Patch configuration retrieved."));
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Patch config fetch cancelled for project {ProjectId}", projectId);
            return StatusCode(499);
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
        [FromBody] PatchConfigRequest config,
        CancellationToken ct)
    {
        try
        {
            await patcherRepo.UpdatePatchConfigAsync(projectId, config, ct);

            return Ok(ApiResponse<string>.Success(
                "Updated",
                "Patch configuration updated successfully."));
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Patch config update (POST) cancelled for project {ProjectId}", projectId);
            return StatusCode(499);
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
        [FromBody] PatchConfigRequest config,
        CancellationToken ct)
    {
        try
        {
            await patcherRepo.UpdatePatchConfigAsync(projectId, config, ct);

            return Ok(ApiResponse<string>.Success(
                "Updated",
                "Patch configuration updated successfully."));
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Patch config update (PUT) cancelled for project {ProjectId}", projectId);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error updating patch config for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<string>.Failure(
                "Config update failed", ["An unexpected error occurred."]));
        }
    }

    private async Task<ResolvedPatchDownload> ResolvePatchDownloadAsync(int patchId, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
        {
            return new ResolvedPatchDownload
            {
                ErrorResult = Unauthorized(ApiResponse<object>.Failure("Unauthorized", ["User ID not found in token."]))
            };
        }

        var patch = await patcherRepo.GetPatchByIdAsync(patchId, userId.Value, ct);
        if (patch == null)
        {
            return new ResolvedPatchDownload
            {
                ErrorResult = NotFound(ApiResponse<object>.Failure(
                    "Patch not found", [$"No patch with ID {patchId} exists."]))
            };
        }

        if (string.IsNullOrWhiteSpace(patch.PatchFilePath))
        {
            return CreateMissingPatchDownload();
        }

        var config = await patcherRepo.GetPatchConfigAsync(patch.ProjectId, ct);
        if (config == null || string.IsNullOrWhiteSpace(config.PatchDownloadPath))
        {
            return CreateMissingPatchDownload();
        }

        string resolvedPatchPath;
        string resolvedStorageRoot;
        try
        {
            resolvedPatchPath = PatchPathSafety.NormalizePath(patch.PatchFilePath, nameof(patch.PatchFilePath));
            if (string.IsNullOrWhiteSpace(config.ProjectRootPath))
            {
                return CreateMissingPatchDownload();
            }

            var projectRootPath = PatchPathSafety.NormalizePath(config.ProjectRootPath, nameof(config.ProjectRootPath));
            resolvedStorageRoot = PatchPathSafety.ResolvePath(projectRootPath, config.PatchDownloadPath, nameof(config.PatchDownloadPath));
        }
        catch
        {
            return CreateMissingPatchDownload();
        }

        try
        {
            PatchPathSafety.EnsurePathIsUnderRoot(resolvedPatchPath, resolvedStorageRoot, nameof(config.PatchDownloadPath));
        }
        catch
        {
            return CreateMissingPatchDownload();
        }

        if (!System.IO.File.Exists(resolvedPatchPath))
        {
            return CreateMissingPatchDownload();
        }

        return new ResolvedPatchDownload
        {
            FilePath = resolvedPatchPath,
            DownloadFileName = Path.GetFileName(resolvedPatchPath)
        };
    }

    private ResolvedPatchDownload CreateMissingPatchDownload()
    {
        return new ResolvedPatchDownload
        {
            ErrorResult = NotFound(ApiResponse<object>.Failure(
                "Patch file not found", ["The patch file no longer exists on disk."]))
        };
    }

    internal sealed class ResolvedPatchDownload
    {
        public string? FilePath { get; init; }
        public string? DownloadFileName { get; init; }
        public IActionResult? ErrorResult { get; init; }
    }
}
