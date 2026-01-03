using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Attributes;
using ActoEngine.WebApi.Services.ContextService;
using ActoEngine.WebApi.Config;
using ActoEngine.WebApi.Extensions;
using Microsoft.Extensions.Options;
using ActoEngine.Domain.Entities;

namespace ActoEngine.WebApi.Controllers;

/// <summary>
/// API endpoints for managing entity context and documentation
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId}/context")]
public class ContextController(
    IContextService contextService,
    ILogger<ContextController> logger,
    IOptions<BatchSettings> batchSettings) : ControllerBase
{
    private readonly IContextService _contextService = contextService;
    private readonly ILogger<ContextController> _logger = logger;
    private readonly BatchSettings _batchSettings = batchSettings.Value;

    #region Context CRUD

    /// <summary>
    /// Get context for an entity
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="entityType">Entity type (TABLE, COLUMN, SP, FUNCTION, VIEW)</param>
    /// <param name="entityId">Entity ID</param>
    [HttpGet("{entityType}/{entityId}")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(ContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContext(
        int projectId,
        string entityType,
        int entityId)
    {
        try
        {
            var context = await _contextService.GetContextAsync(projectId, entityType, entityId);

            if (context == null)
            {
                return NotFound(ApiResponse<object>.Failure($"Context not found for {entityType} with ID {entityId}"));
            }

            return Ok(ApiResponse<ContextResponse>.Success(context, "Context retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred while retrieving context"));
        }
    }

    /// <summary>
    /// Get context for multiple entities in batch
    /// </summary>
    [HttpPost("batch")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(IEnumerable<ContextResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetContextBatch(
        int projectId,
        [FromBody] BatchContextRequest request)
    {
        try
        {
            if (request.Entities == null || request.Entities.Count == 0)
            {
                return Ok(ApiResponse<List<ContextResponse>>.Success([], "No entities provided"));
            }

            // Validate batch size against configured limit with fallback
            var maxBatchSize = _batchSettings.MaxBatchSize > 0 ? _batchSettings.MaxBatchSize : 100;
            if (request.Entities.Count > maxBatchSize)
            {
                return BadRequest(ApiResponse<object>.Failure($"Batch size limited to {maxBatchSize} entities"));
            }

            var contexts = await _contextService.GetContextBatchAsync(projectId, request.Entities);

            return Ok(ApiResponse<IEnumerable<ContextResponse>>.Success(contexts, "Batch context retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch context for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred while retrieving batch context"));
        }
    }

    /// <summary>
    /// Save or update context
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="entityType">Entity type</param>
    /// <param name="entityId">Entity ID</param>
    /// <param name="request">Context data</param>
    [HttpPut("{entityType}/{entityId}")]
    [RequirePermission("Contexts:Update")]
    [ProducesResponseType(typeof(EntityContext), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveContext(
        int projectId,
        string entityType,
        int entityId,
        [FromBody] SaveContextRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var context = await _contextService.SaveContextAsync(
                projectId, entityType, entityId, request, userId.Value);

            return Ok(ApiResponse<EntityContext>.Success(context, "Context saved successfully"));
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain error saving context");
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving context for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred while saving context"));
        }
    }

    /// <summary>
    /// Quick-save context (minimal fields for fast entry)
    /// </summary>
    [HttpPost("quick-save")]
    [RequirePermission("Contexts:Update")]
    [ProducesResponseType(typeof(ApiResponse<ContextResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> QuickSaveContext(
        int projectId,
        [FromBody] QuickSaveRequest request)
    {
        try
        {
            // Validate all required fields
            if (string.IsNullOrWhiteSpace(request.Purpose))
            {
                return BadRequest(ApiResponse<object>.Failure("Purpose is required"));
            }

            if (string.IsNullOrWhiteSpace(request.EntityType))
            {
                return BadRequest(ApiResponse<object>.Failure("EntityType is required"));
            }

            if (request.EntityId <= 0)
            {
                return BadRequest(ApiResponse<object>.Failure("Valid EntityId is required"));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            // Convert to full SaveContextRequest
            var fullRequest = new SaveContextRequest
            {
                Purpose = request.Purpose,
                CriticalityLevel = request.CriticalityLevel ?? 3
            };

            var context = await _contextService.SaveContextAsync(
                projectId,
                request.EntityType,
                request.EntityId,
                fullRequest,
                userId.Value);

            // Calculate completeness
            var completeness = CalculateCompleteness(context);

            return Ok(ApiResponse<QuickSaveResponse>.Success(
                new QuickSaveResponse
                {
                    Context = context,
                    CompletenessScore = completeness,
                    Message = $"Context saved ({completeness}% complete)"
                },
                "Context saved successfully"));
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain error in quick-save");
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in quick-save");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    private static int CalculateCompleteness(EntityContext context)
    {
        var fields = new[]
        {
        context.Purpose,
        context.BusinessImpact,
        context.DataOwner,
        context.BusinessDomain
    };
        var filled = fields.Count(f => !string.IsNullOrWhiteSpace(f));
        return (int)Math.Round((filled / (double)fields.Length) * 100);
    }
    /// <summary>
    /// Mark context as reviewed (fresh)
    /// </summary>
    [HttpPost("{entityType}/{entityId}/mark-reviewed")]
    [RequirePermission("Contexts:Update")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkContextReviewed(
        int projectId,
        string entityType,
        int entityId)
    {
        try
        {
            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            await _contextService.MarkContextFreshAsync(projectId, entityType, entityId, userId.Value);

            return Ok(ApiResponse<object>.Success(new { }, "Context marked as reviewed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking context as reviewed");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    #endregion

    #region Experts Management

    /// <summary>
    /// Add expert to entity
    /// </summary>
    [HttpPost("{entityType}/{entityId}/experts")]
    [RequirePermission("Contexts:Update")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddExpert(
        int projectId,
        string entityType,
        int entityId,
        [FromBody] AddExpertRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var currentUserId = HttpContext.GetUserId();
            if (currentUserId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            await _contextService.AddExpertAsync(
                projectId,
                entityType,
                entityId,
                request.UserId,
                request.ExpertiseLevel,
                request.Notes,
                currentUserId.Value);

            return Ok(ApiResponse<object>.Success(new { }, "Expert added successfully"));
        }
        catch (DomainException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding expert");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Remove expert from entity
    /// </summary>
    [HttpDelete("{entityType}/{entityId}/experts/{userId}")]
    [RequirePermission("Contexts:Update")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveExpert(
        int projectId,
        string entityType,
        int entityId,
        int userId)
    {
        try
        {
            await _contextService.RemoveExpertAsync(projectId, entityType, entityId, userId);
            return Ok(ApiResponse<object>.Success(new { }, "Expert removed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing expert");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Get user's expertise (entities they're expert on)
    /// </summary>
    [HttpGet("users/{userId}/expertise")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(List<dynamic>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserExpertise(
        int projectId,
        int userId)
    {
        try
        {
            var expertise = await _contextService.GetUserExpertiseAsync(userId, projectId);
            return Ok(ApiResponse<object>.Success(expertise, "User expertise retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user expertise");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    #endregion

    #region Suggestions

    /// <summary>
    /// Get context suggestions for an entity
    /// </summary>
    [HttpGet("{entityType}/{entityId}/suggestions")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(ContextSuggestions), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestions(
        int projectId,
        string entityType,
        int entityId)
    {
        try
        {
            var suggestions = await _contextService.GetContextSuggestionsAsync(
                projectId, entityType, entityId);

            return Ok(ApiResponse<ContextSuggestions>.Success(suggestions, "Suggestions retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestions");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    #endregion

    #region Statistics & Insights

    /// <summary>
    /// Get context coverage statistics
    /// </summary>
    [HttpGet("statistics/coverage")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(List<ContextCoverageStats>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContextCoverage(
        int projectId)
    {
        try
        {
            var stats = await _contextService.GetContextCoverageAsync(projectId);
            return Ok(ApiResponse<IEnumerable<ContextCoverageStats>>.Success(stats, "Context coverage retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context coverage");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Get entities with stale context
    /// </summary>
    [HttpGet("statistics/stale")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(List<dynamic>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaleEntities(int projectId)
    {
        try
        {
            var entities = await _contextService.GetStaleContextEntitiesAsync(projectId);
            return Ok(ApiResponse<object>.Success(entities, "Stale entities retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stale entities");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Get top documented entities
    /// </summary>
    [HttpGet("statistics/top-documented")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(List<dynamic>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopDocumented(
        int projectId,
        [FromQuery] int limit = 10)
    {
        try
        {
            var entities = await _contextService.GetTopDocumentedEntitiesAsync(projectId, limit);
            return Ok(ApiResponse<object>.Success(entities, "Top documented entities retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top documented entities");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Get critical undocumented entities
    /// </summary>
    [HttpGet("statistics/critical-undocumented")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(List<dynamic>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCriticalUndocumented(int projectId)
    {
        try
        {
            var entities = await _contextService.GetCriticalUndocumentedAsync(projectId);
            return Ok(ApiResponse<object>.Success(entities, "Critical undocumented entities retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting critical undocumented entities");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Get entities missing context (prioritized by usage)
    /// </summary>
    [HttpGet("gaps")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(List<ContextGap>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContextGaps(
        int projectId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var gaps = await _contextService.GetContextGapsAsync(projectId, limit, cancellationToken);
            return Ok(ApiResponse<IEnumerable<ContextGap>>.Success(gaps, "Context gaps retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context gaps");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Get context dashboard data
    /// </summary>
    [HttpGet("dashboard")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(int projectId)
    {
        try
        {
            var coverage = await _contextService.GetContextCoverageAsync(projectId);
            var stale = await _contextService.GetStaleContextEntitiesAsync(projectId);
            var topDocumented = await _contextService.GetTopDocumentedEntitiesAsync(projectId, 5);
            var criticalUndocumented = await _contextService.GetCriticalUndocumentedAsync(projectId);

            return Ok(ApiResponse<object>.Success(new
            {
                coverage,
                staleCount = stale.Count,
                staleEntities = stale.Take(5),
                topDocumented,
                criticalUndocumented = criticalUndocumented.Take(5),
                lastUpdated = DateTime.UtcNow
            }, "Dashboard data retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard data");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Bulk import context entries
    /// </summary>
    [HttpPost("bulk-import")]
    [RequirePermission("Contexts:Create")]
    public async Task<IActionResult> BulkImportContext(
        int projectId,
        [FromBody] List<BulkContextEntry> entries)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var results = await _contextService.BulkImportContextAsync(projectId, entries, userId.Value);

            return Ok(ApiResponse<IEnumerable<BulkImportResult>>.Success(results, "Bulk import completed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk importing context");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred during bulk import"));
        }
    }

    #endregion

    #region Review Management

    /// <summary>
    /// Create review request
    /// </summary>
    [HttpPost("review-requests")]
    [RequirePermission("Contexts:Update")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReviewRequest(
        [FromBody] CreateReviewRequestModel request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var requestId = await _contextService.CreateReviewRequestAsync(
                request.EntityType,
                request.EntityId,
                userId.Value,
                request.AssignedTo,
                request.Reason);

            return Ok(ApiResponse<object>.Success(new { requestId }, "Review request created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review request");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Get pending review requests
    /// </summary>
    [HttpGet("review-requests/pending")]
    [RequirePermission("Contexts:Read")]
    [ProducesResponseType(typeof(List<ContextReviewRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReviewRequests(
        [FromQuery] int? assignedTo = null)
    {
        try
        {
            var requests = await _contextService.GetPendingReviewRequestsAsync(assignedTo);
            return Ok(ApiResponse<IEnumerable<ContextReviewRequest>>.Success(requests, "Pending review requests retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting review requests");
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get current user ID from auth context
    /// Adjust this based on your authentication implementation
    /// </summary>
    private int GetCurrentUserId()
    {
        // Option 1: If you store UserId in claims
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (userIdClaim != null && int.TryParse(userIdClaim, out int userId))
        {
            return userId;
        }

        // Option 2: If you store it differently
        var nameIdentifier = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (nameIdentifier != null && int.TryParse(nameIdentifier, out int uid))
        {
            return uid;
        }

        // Fallback - adjust based on your needs
        throw new UnauthorizedAccessException("User ID not found in authentication context");
    }

    #endregion
}