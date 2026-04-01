using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ActoEngine.WebApi.Features.Snippets;

public interface ISnippetRepository
{
    Task<PaginatedResult<SnippetListResponse>> GetAllAsync(SnippetListParams listParams, int currentUserId, CancellationToken cancellationToken = default);
    Task<SnippetDetailResponse?> GetByIdAsync(int snippetId, int currentUserId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Snippet snippet, List<string> tags, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Snippet snippet, List<string> tags, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int snippetId, int userId, CancellationToken cancellationToken = default);
    Task<bool> IncrementCopyCountAsync(int snippetId, CancellationToken cancellationToken = default);
    Task<bool> ToggleFavoriteAsync(int snippetId, int userId, CancellationToken cancellationToken = default);
    Task<SnippetFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}

public class SnippetRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<SnippetRepository> logger)
    : BaseRepository(connectionFactory, logger), ISnippetRepository
{
    public async Task<PaginatedResult<SnippetListResponse>> GetAllAsync(
        SnippetListParams listParams, int currentUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var whereClauses = new List<string> { "s.IsActive = 1" };
            var parameters = new DynamicParameters();
            parameters.Add("CurrentUserId", currentUserId);

            // Search strategy:
            //   Title / Description  →  Full-Text Search (CONTAINS prefix term) — natural language fields
            //   Code                 →  LIKE '%fragment%'                        — infix substring match
            // FTS does NOT support infix matching, so "AUDITLOG" would never find
            // "COMMON_AUDITLOG_SUMMARY_CRUD" via CONTAINS. LIKE handles this correctly.
            if (!string.IsNullOrWhiteSpace(listParams.Search))
            {
                var rawTerm = listParams.Search.Trim();
                var ftsToken = rawTerm.Replace("\"", "").Replace("'", "''");
                var ftsPrefix = $"\"{ftsToken}*\"";
                var likeTerm = $"%{rawTerm}%";

                whereClauses.Add(@"(
                    (LEN(@SearchTerm) > 0 AND CONTAINS((s.Title, s.Description), @FtsSearch))
                    OR s.Code LIKE @LikeTerm
                )");
                parameters.Add("SearchTerm", rawTerm);
                parameters.Add("FtsSearch", ftsPrefix);
                parameters.Add("LikeTerm", likeTerm);
            }

            if (!string.IsNullOrWhiteSpace(listParams.Language))
            {
                whereClauses.Add("s.Language = @Language");
                parameters.Add("Language", listParams.Language);
            }

            if (!string.IsNullOrWhiteSpace(listParams.Tag))
            {
                whereClauses.Add("EXISTS (SELECT 1 FROM SnippetTags st WHERE st.SnippetId = s.SnippetId AND st.TagName = @Tag)");
                parameters.Add("Tag", listParams.Tag);
            }

            var whereClause = string.Join(" AND ", whereClauses);

            var orderBy = listParams.SortBy switch
            {
                "popular" => "s.CopyCount DESC, s.CreatedAt DESC",
                "favorites" => "FavoriteCount DESC, s.CreatedAt DESC",
                "title" => "s.Title ASC",
                _ => "s.CreatedAt DESC"
            };

            var offset = (listParams.Page - 1) * listParams.PageSize;
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", listParams.PageSize);

            var countSql = $@"
                SELECT COUNT(*)
                FROM Snippets s
                WHERE {whereClause}";

            var listSql = $@"
                SELECT s.SnippetId, s.Title, s.Description, s.Language,
                       s.CopyCount, s.CreatedBy, s.CreatedAt,
                       u.FullName AS AuthorName,
                       (SELECT COUNT(*) FROM SnippetFavorites WHERE SnippetId = s.SnippetId) AS FavoriteCount,
                       CAST(CASE WHEN EXISTS (
                           SELECT 1 FROM SnippetFavorites WHERE SnippetId = s.SnippetId AND UserId = @CurrentUserId
                       ) THEN 1 ELSE 0 END AS BIT) AS IsFavorited
                FROM Snippets s
                INNER JOIN Users u ON s.CreatedBy = u.UserID
                WHERE {whereClause}
                ORDER BY {orderBy}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            var totalCount = await connection.QuerySingleAsync<int>(
                new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

            var snippets = (await connection.QueryAsync<SnippetListResponse>(
                new CommandDefinition(listSql, parameters, cancellationToken: cancellationToken))).ToList();

            // Batch-fetch tags for all snippets
            if (snippets.Count > 0)
            {
                var snippetIds = snippets.Select(s => s.SnippetId).ToArray();
                var tags = await connection.QueryAsync<SnippetTag>(
                    new CommandDefinition(SnippetQueries.GetTagsBySnippetIds, new { SnippetIds = snippetIds }, cancellationToken: cancellationToken));

                var tagsBySnippetId = tags.GroupBy(t => t.SnippetId)
                    .ToDictionary(g => g.Key, g => g.Select(t => t.TagName).ToList());

                foreach (var snippet in snippets)
                {
                    snippet.Tags = tagsBySnippetId.GetValueOrDefault(snippet.SnippetId, []);
                }
            }

            return new PaginatedResult<SnippetListResponse>
            {
                Items = snippets,
                TotalCount = totalCount,
                Page = listParams.Page,
                PageSize = listParams.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving snippets");
            throw;
        }
    }

    public async Task<SnippetDetailResponse?> GetByIdAsync(int snippetId, int currentUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var snippet = await QueryFirstOrDefaultAsync<SnippetDetailResponse>(
                SnippetQueries.GetById,
                new { SnippetId = snippetId, CurrentUserId = currentUserId },
                cancellationToken);

            if (snippet == null) return null;

            var tags = await QueryAsync<SnippetTag>(
                SnippetQueries.GetTagsBySnippetId,
                new { SnippetId = snippetId },
                cancellationToken);

            snippet.Tags = tags.Select(t => t.TagName).ToList();
            return snippet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task<int> CreateAsync(Snippet snippet, List<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                var snippetId = await connection.QuerySingleAsync<int>(
                    SnippetQueries.Insert,
                    new
                    {
                        snippet.Title,
                        snippet.Description,
                        snippet.Code,
                        snippet.Language,
                        snippet.Notes,
                        snippet.CreatedBy,
                        CreatedAt = DateTime.UtcNow
                    },
                    transaction);

                foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    await connection.ExecuteAsync(
                        SnippetQueries.InsertTag,
                        new { SnippetId = snippetId, TagName = tag.Trim() },
                        transaction);
                }

                return snippetId;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snippet {Title}", snippet.Title);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(Snippet snippet, List<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                var rowsAffected = await connection.ExecuteAsync(
                    SnippetQueries.Update,
                    new
                    {
                        snippet.SnippetId,
                        snippet.Title,
                        snippet.Description,
                        snippet.Code,
                        snippet.Language,
                        snippet.Notes,
                        snippet.UpdatedBy,
                        UpdatedAt = DateTime.UtcNow
                    },
                    transaction);

                if (rowsAffected == 0) return false;

                await connection.ExecuteAsync(
                    SnippetQueries.DeleteTagsBySnippetId,
                    new { snippet.SnippetId },
                    transaction);

                foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    await connection.ExecuteAsync(
                        SnippetQueries.InsertTag,
                        new { snippet.SnippetId, TagName = tag.Trim() },
                        transaction);
                }

                return true;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating snippet {SnippetId}", snippet.SnippetId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int snippetId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rowsAffected = await ExecuteAsync(
                SnippetQueries.SoftDelete,
                new { SnippetId = snippetId, UpdatedBy = userId, UpdatedAt = DateTime.UtcNow },
                cancellationToken);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task<bool> IncrementCopyCountAsync(int snippetId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rowsAffected = await ExecuteAsync(
                SnippetQueries.IncrementCopyCount,
                new { SnippetId = snippetId },
                cancellationToken);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing copy count for snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task<bool> ToggleFavoriteAsync(int snippetId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            var exists = await connection.QuerySingleAsync<int>(
                new CommandDefinition(SnippetQueries.ToggleFavoriteCheck, new { SnippetId = snippetId, UserId = userId }, cancellationToken: cancellationToken));

            if (exists > 0)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(SnippetQueries.RemoveFavorite, new { SnippetId = snippetId, UserId = userId }, cancellationToken: cancellationToken));
                return false; // unfavorited
            }
            else
            {
                try
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(SnippetQueries.AddFavorite, new { SnippetId = snippetId, UserId = userId }, cancellationToken: cancellationToken));
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    // Concurrent insert already added the favorite — treat as success
                }
                return true; // favorited
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite for snippet {SnippetId}", snippetId);
            throw;
        }
    }

    public async Task<SnippetFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = await QueryAsync<string>(SnippetQueries.GetAllTags, cancellationToken: cancellationToken);
            var languages = await QueryAsync<string>(SnippetQueries.GetAllLanguages, cancellationToken: cancellationToken);

            return new SnippetFilterOptions
            {
                Tags = tags.ToList(),
                Languages = languages.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving snippet filter options");
            throw;
        }
    }
}
