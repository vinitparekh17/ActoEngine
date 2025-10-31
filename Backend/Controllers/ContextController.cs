using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.ContextService;
using ActoEngine.WebApi.Config;

namespace ActoEngine.WebApi.Controllers;

/// <summary>
/// API endpoints for managing entity context and documentation
/// </summary>
[ApiController]
[Route("api/projects/{projectId}/context")]
public class ContextController : ControllerBase
{
    private readonly ContextService _contextService;
    private readonly ILogger<ContextController> _logger;

    public ContextController(
        ContextService contextService,
        ILogger<ContextController> logger)
    {
        _contextService = contextService;
        _logger = logger;
    }

    #region Context CRUD

    /// <summary>
    /// Get context for an entity
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="entityType">Entity type (TABLE, COLUMN, SP, FUNCTION, VIEW)</param>
    /// <param name="entityId">Entity ID</param>
    [HttpGet("{entityType}/{entityId}")]
    [ProducesResponseType(typeof(ContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContextResponse>> GetContext(
        int projectId,
        string entityType,
        int entityId)
    {
        try
        {
            var context = await _contextService.GetContextAsync(projectId, entityType, entityId);
            
            if (context == null)
                return NotFound(new { message = $"Context not found for {entityType} with ID {entityId}" });

            return Ok(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, new { message = "An error occurred while retrieving context" });
        }
    }

    /// <summary>
    /// Save or update context
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="entityType">Entity type</param>
    /// <param name="entityId">Entity ID</param>
    /// <param name="request">Context data</param>
    [HttpPost("{entityType}/{entityId}")]
    [ProducesResponseType(typeof(EntityContext), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EntityContext>> SaveContext(
        int projectId,
        string entityType,
        int entityId,
        [FromBody] SaveContextRequest request)
    {
        try
        {
            // Get user ID from auth context (adjust based on your auth implementation)
            var userId = GetCurrentUserId();

            var context = await _contextService.SaveContextAsync(
                projectId, entityType, entityId, request, userId);

            return Ok(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain error saving context");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving context for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, new { message = "An error occurred while saving context" });
        }
    }

    /// <summary>
    /// Mark context as reviewed (fresh)
    /// </summary>
    [HttpPost("{entityType}/{entityId}/mark-reviewed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> MarkContextReviewed(
        int projectId,
        string entityType,
        int entityId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _contextService.MarkContextFreshAsync(projectId, entityType, entityId, userId);
            
            return Ok(new { message = "Context marked as reviewed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking context as reviewed");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Experts Management

    /// <summary>
    /// Add expert to entity
    /// </summary>
    [HttpPost("{entityType}/{entityId}/experts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddExpert(
        int projectId,
        string entityType,
        int entityId,
        [FromBody] AddExpertRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();

            await _contextService.AddExpertAsync(
                projectId,
                entityType,
                entityId,
                request.UserId,
                request.ExpertiseLevel,
                request.Notes,
                currentUserId);

            return Ok(new { message = "Expert added successfully" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding expert");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove expert from entity
    /// </summary>
    [HttpDelete("{entityType}/{entityId}/experts/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RemoveExpert(
        int projectId,
        string entityType,
        int entityId,
        int userId)
    {
        try
        {
            await _contextService.RemoveExpertAsync(projectId, entityType, entityId, userId);
            return Ok(new { message = "Expert removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing expert");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get user's expertise (entities they're expert on)
    /// </summary>
    [HttpGet("users/{userId}/expertise")]
    [ProducesResponseType(typeof(List<dynamic>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUserExpertise(
        int projectId,
        int userId)
    {
        try
        {
            var expertise = await _contextService.GetUserExpertiseAsync(userId, projectId);
            return Ok(expertise);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user expertise");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Suggestions

    /// <summary>
    /// Get context suggestions for an entity
    /// </summary>
    [HttpGet("{entityType}/{entityId}/suggestions")]
    [ProducesResponseType(typeof(ContextSuggestions), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContextSuggestions>> GetSuggestions(
        int projectId,
        string entityType,
        int entityId)
    {
        try
        {
            var suggestions = await _contextService.GetContextSuggestionsAsync(
                projectId, entityType, entityId);

            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestions");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Statistics & Insights

    /// <summary>
    /// Get context coverage statistics
    /// </summary>
    [HttpGet("statistics/coverage")]
    [ProducesResponseType(typeof(List<ContextCoverageStats>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ContextCoverageStats>>> GetContextCoverage(
        int projectId)
    {
        try
        {
            var stats = await _contextService.GetContextCoverageAsync(projectId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context coverage");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get entities with stale context
    /// </summary>
    [HttpGet("statistics/stale")]
    [ProducesResponseType(typeof(List<dynamic>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStaleEntities(int projectId)
    {
        try
        {
            var entities = await _contextService.GetStaleContextEntitiesAsync(projectId);
            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stale entities");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get top documented entities
    /// </summary>
    [HttpGet("statistics/top-documented")]
    [ProducesResponseType(typeof(List<dynamic>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTopDocumented(
        int projectId,
        [FromQuery] int limit = 10)
    {
        try
        {
            var entities = await _contextService.GetTopDocumentedEntitiesAsync(projectId, limit);
            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top documented entities");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get critical undocumented entities
    /// </summary>
    [HttpGet("statistics/critical-undocumented")]
    [ProducesResponseType(typeof(List<dynamic>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetCriticalUndocumented(int projectId)
    {
        try
        {
            var entities = await _contextService.GetCriticalUndocumentedAsync(projectId);
            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting critical undocumented entities");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get context dashboard data
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetDashboard(int projectId)
    {
        try
        {
            var coverage = await _contextService.GetContextCoverageAsync(projectId);
            var stale = await _contextService.GetStaleContextEntitiesAsync(projectId);
            var topDocumented = await _contextService.GetTopDocumentedEntitiesAsync(projectId, 5);
            var criticalUndocumented = await _contextService.GetCriticalUndocumentedAsync(projectId);

            return Ok(new
            {
                coverage,
                staleCount = stale.Count,
                staleEntities = stale.Take(5),
                topDocumented,
                criticalUndocumented = criticalUndocumented.Take(5),
                lastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard data");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Bulk import context entries
    /// </summary>
    [HttpPost("bulk-import")]
    [ProducesResponseType(typeof(List<BulkImportResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BulkImportResult>>> BulkImportContext(
        int projectId,
        [FromBody] List<BulkContextEntry> entries)
    {
        try
        {
            var userId = GetCurrentUserId();
            var results = await _contextService.BulkImportContextAsync(projectId, entries, userId);
            
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk importing context");
            return StatusCode(500, new { message = "An error occurred during bulk import" });
        }
    }

    #endregion

    #region Review Management

    /// <summary>
    /// Create review request
    /// </summary>
    [HttpPost("review-requests")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> CreateReviewRequest(
        [FromBody] dynamic request)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            var requestId = await _contextService.CreateReviewRequestAsync(
                request.entityType,
                request.entityId,
                userId,
                request.assignedTo,
                request.reason);

            return Ok(new { requestId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review request");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get pending review requests
    /// </summary>
    [HttpGet("review-requests/pending")]
    [ProducesResponseType(typeof(List<ContextReviewRequest>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ContextReviewRequest>>> GetPendingReviewRequests(
        [FromQuery] int? assignedTo = null)
    {
        try
        {
            var requests = await _contextService.GetPendingReviewRequestsAsync(assignedTo);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting review requests");
            return StatusCode(500, new { message = "An error occurred" });
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
            return userId;

        // Option 2: If you store it differently
        var nameIdentifier = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (nameIdentifier != null && int.TryParse(nameIdentifier, out int uid))
            return uid;

        // Fallback - adjust based on your needs
        throw new UnauthorizedAccessException("User ID not found in authentication context");
    }

    #endregion
}