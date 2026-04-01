namespace ActoEngine.WebApi.Features.Snippets;

public static class SnippetQueries
{
    public const string GetById = @"
        SELECT s.SnippetId, s.Title, s.Description, s.Code, s.Language, s.Notes,
               s.CopyCount, s.CreatedBy, s.UpdatedBy, s.CreatedAt, s.UpdatedAt, s.IsActive,
               u.FullName AS AuthorName,
               (SELECT COUNT(*) FROM SnippetFavorites WHERE SnippetId = s.SnippetId) AS FavoriteCount,
               CAST(CASE WHEN EXISTS (
                   SELECT 1 FROM SnippetFavorites WHERE SnippetId = s.SnippetId AND UserId = @CurrentUserId
               ) THEN 1 ELSE 0 END AS BIT) AS IsFavorited
        FROM Snippets s
        INNER JOIN Users u ON s.CreatedBy = u.UserID
        WHERE s.SnippetId = @SnippetId AND s.IsActive = 1";

    public const string GetTagsBySnippetId = @"
        SELECT SnippetTagId, SnippetId, TagName
        FROM SnippetTags
        WHERE SnippetId = @SnippetId";

    public const string GetTagsBySnippetIds = @"
        SELECT SnippetTagId, SnippetId, TagName
        FROM SnippetTags
        WHERE SnippetId IN @SnippetIds";

    public const string Insert = @"
        INSERT INTO Snippets (Title, Description, Code, Language, Notes, CreatedBy, CreatedAt)
        VALUES (@Title, @Description, @Code, @Language, @Notes, @CreatedBy, @CreatedAt);
        SELECT CAST(SCOPE_IDENTITY() AS INT);";

    public const string InsertTag = @"
        INSERT INTO SnippetTags (SnippetId, TagName)
        VALUES (@SnippetId, @TagName);";

    public const string DeleteTagsBySnippetId = @"
        DELETE FROM SnippetTags WHERE SnippetId = @SnippetId;";

    public const string Update = @"
        UPDATE Snippets
        SET Title = @Title,
            Description = @Description,
            Code = @Code,
            Language = @Language,
            Notes = @Notes,
            UpdatedBy = @UpdatedBy,
            UpdatedAt = @UpdatedAt
        WHERE SnippetId = @SnippetId AND IsActive = 1";

    public const string SoftDelete = @"
        UPDATE Snippets
        SET IsActive = 0,
            UpdatedBy = @UpdatedBy,
            UpdatedAt = @UpdatedAt
        WHERE SnippetId = @SnippetId AND IsActive = 1";

    public const string IncrementCopyCount = @"
        UPDATE Snippets
        SET CopyCount = CopyCount + 1
        WHERE SnippetId = @SnippetId AND IsActive = 1";

    public const string ToggleFavoriteCheck = @"
        SELECT CASE WHEN EXISTS(SELECT 1 FROM SnippetFavorites WHERE SnippetId = @SnippetId AND UserId = @UserId) THEN 1 ELSE 0 END";

    public const string AddFavorite = @"
        INSERT INTO SnippetFavorites (SnippetId, UserId, CreatedAt)
        VALUES (@SnippetId, @UserId, GETUTCDATE())";

    public const string RemoveFavorite = @"
        DELETE FROM SnippetFavorites
        WHERE SnippetId = @SnippetId AND UserId = @UserId";

    public const string GetAllTags = @"
        SELECT DISTINCT TagName
        FROM SnippetTags st
        INNER JOIN Snippets s ON st.SnippetId = s.SnippetId
        WHERE s.IsActive = 1
        ORDER BY TagName";

    public const string GetAllLanguages = @"
        SELECT DISTINCT Language
        FROM Snippets
        WHERE IsActive = 1
        ORDER BY Language";
}
