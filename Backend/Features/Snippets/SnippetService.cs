namespace ActoEngine.WebApi.Features.Snippets;

public interface ISnippetService
{
    Task<PaginatedResult<SnippetListResponse>> GetSnippetsAsync(SnippetListParams listParams, int currentUserId);
    Task<SnippetDetailResponse?> GetSnippetByIdAsync(int snippetId, int currentUserId);
    Task<SnippetDetailResponse> CreateSnippetAsync(CreateSnippetRequest request, int userId);
    Task<bool> UpdateSnippetAsync(int snippetId, UpdateSnippetRequest request, int userId, bool isAdmin);
    Task<bool> DeleteSnippetAsync(int snippetId, int userId, bool isAdmin);
    Task IncrementCopyCountAsync(int snippetId);
    Task<bool> ToggleFavoriteAsync(int snippetId, int userId);
    Task<SnippetFilterOptions> GetFilterOptionsAsync();
}

public class SnippetService(
    ISnippetRepository snippetRepository,
    ILogger<SnippetService> logger) : ISnippetService
{
    private readonly ISnippetRepository _snippetRepository = snippetRepository;
    private readonly ILogger<SnippetService> _logger = logger;

    public async Task<PaginatedResult<SnippetListResponse>> GetSnippetsAsync(SnippetListParams listParams, int currentUserId)
    {
        try
        {
            return await _snippetRepository.GetAllAsync(listParams, currentUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving snippets");
            throw;
        }
    }

    public async Task<SnippetDetailResponse?> GetSnippetByIdAsync(int snippetId, int currentUserId)
    {
        try
        {
            return await _snippetRepository.GetByIdAsync(snippetId, currentUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task<SnippetDetailResponse> CreateSnippetAsync(CreateSnippetRequest request, int userId)
    {
        try
        {
            var snippet = new Snippet
            {
                Title = request.Title,
                Description = request.Description,
                Code = request.Code,
                Language = request.Language,
                Notes = request.Notes,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            var snippetId = await _snippetRepository.CreateAsync(snippet, request.Tags);
            _logger.LogInformation("Created snippet {SnippetId} by user {UserId}", snippetId, userId);

            var created = await _snippetRepository.GetByIdAsync(snippetId, userId);
            return created!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snippet");
            throw;
        }
    }

    public async Task<bool> UpdateSnippetAsync(int snippetId, UpdateSnippetRequest request, int userId, bool isAdmin)
    {
        try
        {
            var existing = await _snippetRepository.GetByIdAsync(snippetId, userId);
            if (existing == null) return false;

            if (existing.CreatedBy != userId && !isAdmin)
            {
                _logger.LogWarning("User {UserId} attempted to update snippet {SnippetId} owned by {OwnerId}",
                    userId, snippetId, existing.CreatedBy);
                return false;
            }

            var snippet = new Snippet
            {
                SnippetId = snippetId,
                Title = request.Title,
                Description = request.Description,
                Code = request.Code,
                Language = request.Language,
                Notes = request.Notes,
                UpdatedBy = userId,
                UpdatedAt = DateTime.UtcNow
            };

            return await _snippetRepository.UpdateAsync(snippet, request.Tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task<bool> DeleteSnippetAsync(int snippetId, int userId, bool isAdmin)
    {
        try
        {
            var existing = await _snippetRepository.GetByIdAsync(snippetId, userId);
            if (existing == null) return false;

            if (existing.CreatedBy != userId && !isAdmin)
            {
                _logger.LogWarning("User {UserId} attempted to delete snippet {SnippetId} owned by {OwnerId}",
                    userId, snippetId, existing.CreatedBy);
                return false;
            }

            return await _snippetRepository.DeleteAsync(snippetId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task IncrementCopyCountAsync(int snippetId)
    {
        try
        {
            await _snippetRepository.IncrementCopyCountAsync(snippetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing copy count for snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task<bool> ToggleFavoriteAsync(int snippetId, int userId)
    {
        try
        {
            return await _snippetRepository.ToggleFavoriteAsync(snippetId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite for snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task<SnippetFilterOptions> GetFilterOptionsAsync()
    {
        try
        {
            return await _snippetRepository.GetFilterOptionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving snippet filter options");
            throw;
        }
    }
}
