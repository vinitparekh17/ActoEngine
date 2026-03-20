using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Api.Attributes;
using ActoEngine.WebApi.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Features.Patcher;

[ApiController]
[Authorize]
[RequirePermission("StoredProcedures:Create")]
[Route("api/projects/{projectId:int}")]
public class MappingsController(
    IPageMappingRepository pageMappingRepository,
    ILogger<MappingsController> logger) : ControllerBase
{
    [HttpPost("mapping-detections")]
    public async Task<ActionResult<ApiResponse<object>>> UpsertMappingDetections(
        int projectId,
        [FromBody] List<MappingDetectionRequest> detections,
        CancellationToken ct)
    {
        if (detections == null || detections.Count == 0)
        {
            return BadRequest(ApiResponse<object>.Failure("At least one mapping detection is required."));
        }

        try
        {
            var result = await pageMappingRepository.UpsertDetectionsAsync(projectId, detections, ct);
            return Ok(ApiResponse<object>.Success(
                new { Received = result.Received, Inserted = result.UniqueCount },
                $"Processed {result.UniqueCount} unique detection{(result.UniqueCount == 1 ? "" : "s")} ({result.Received} received)."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid mapping-detection request for project {ProjectId}", projectId);
            return BadRequest(ApiResponse<object>.Failure("Invalid mapping detection payload."));
        }
    }

    [HttpGet("mappings")]
    public async Task<ActionResult<ApiResponse<List<PageMappingDto>>>> GetMappings(
        int projectId,
        [FromQuery] string? status,
        [FromQuery] string? domainName,
        [FromQuery] string? pageName,
        CancellationToken ct)
    {
        try
        {
            var mappings = await pageMappingRepository.GetMappingsAsync(projectId, status, domainName, pageName, ct);
            return Ok(ApiResponse<List<PageMappingDto>>.Success(mappings, $"Found {mappings.Count} mapping records."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid mapping query for project {ProjectId}", projectId);
            return BadRequest(ApiResponse<List<PageMappingDto>>.Failure("Invalid mapping query."));
        }
    }

    [HttpPatch("mappings/{mappingId:int}")]
    public async Task<ActionResult<ApiResponse<PageMappingDto>>> UpdateMapping(
        int projectId,
        int mappingId,
        [FromBody] UpdateMappingRequest request,
        CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<PageMappingDto>.Failure("Update payload is required."));
        }

        // Normalize whitespace-only values to null so they don't count as mutations
        if (string.IsNullOrWhiteSpace(request.MappingType)) request.MappingType = null;
        if (string.IsNullOrWhiteSpace(request.DomainName)) request.DomainName = null;
        if (string.IsNullOrWhiteSpace(request.PageName)) request.PageName = null;
        if (string.IsNullOrWhiteSpace(request.StoredProcedure)) request.StoredProcedure = null;

        var hasMutation =
            request.Status != null ||
            request.MappingType != null ||
            request.DomainName != null ||
            request.PageName != null ||
            request.StoredProcedure != null;

        if (!hasMutation)
        {
            return BadRequest(ApiResponse<PageMappingDto>.Failure("No mapping fields provided for update."));
        }

        var userId = HttpContext.GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<PageMappingDto>.Failure("User not authenticated."));
        }

        try
        {
            var updated = await pageMappingRepository.UpdateMappingAsync(projectId, mappingId, request, userId, ct);
            if (!updated)
            {
                return NotFound(ApiResponse<PageMappingDto>.Failure("Mapping not found."));
            }

            var mapping = await pageMappingRepository.GetByIdAsync(projectId, mappingId, ct);
            if (mapping == null)
            {
                return NotFound(ApiResponse<PageMappingDto>.Failure("Mapping not found."));
            }

            return Ok(ApiResponse<PageMappingDto>.Success(mapping, "Mapping updated successfully."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Mapping update failed for project {ProjectId}, mapping {MappingId}", projectId, mappingId);
            return BadRequest(ApiResponse<PageMappingDto>.Failure("Mapping update failed."));
        }
    }

    [HttpPatch("mappings/bulk")]
    public async Task<ActionResult<ApiResponse<object>>> BulkUpdateMappings(
        int projectId,
        [FromBody] BulkMappingActionRequest request,
        CancellationToken ct)
    {
        if (request == null || request.Ids == null || request.Ids.Count == 0)
        {
            return BadRequest(ApiResponse<object>.Failure("At least one mapping ID is required."));
        }

        var userId = HttpContext.GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.Failure("User not authenticated."));
        }

        try
        {
            var count = await pageMappingRepository.BulkUpdateStatusAsync(projectId, request.Ids, request.Action, userId.Value, ct);
            return Ok(ApiResponse<object>.Success(new { Count = count }, $"Updated {count} mapping records."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Bulk mapping update failed for project {ProjectId}", projectId);
            return BadRequest(ApiResponse<object>.Failure("Bulk mapping update failed."));
        }
    }

    [HttpDelete("mappings/{mappingId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCandidateMapping(int projectId, int mappingId, CancellationToken ct)
    {
        var existing = await pageMappingRepository.GetByIdAsync(projectId, mappingId, ct);
        if (existing == null)
        {
            return NotFound(ApiResponse<object>.Failure("Mapping not found."));
        }

        if (!existing.Status.Equals(PageMappingConstants.StatusCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<object>.Failure("Only candidate mappings can be deleted."));
        }

        var deleted = await pageMappingRepository.DeleteCandidateAsync(projectId, mappingId, ct);
        if (!deleted)
        {
            return NotFound(ApiResponse<object>.Failure("Mapping not found."));
        }

        return Ok(ApiResponse<object>.Success(new { MappingId = mappingId }, "Candidate mapping deleted."));
    }
}
