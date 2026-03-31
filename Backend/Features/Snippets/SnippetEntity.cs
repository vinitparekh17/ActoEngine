namespace ActoEngine.WebApi.Features.Snippets;

public class Snippet
{
    public int SnippetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int CopyCount { get; set; }
    public int CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SnippetTag
{
    public int SnippetTagId { get; set; }
    public int SnippetId { get; set; }
    public string TagName { get; set; } = string.Empty;
}

public class SnippetFavorite
{
    public int SnippetFavoriteId { get; set; }
    public int SnippetId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
