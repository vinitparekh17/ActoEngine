using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Snippets;

public class CreateSnippetRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
    public string Title { get; set; } = default!;

    [Required(ErrorMessage = "Code is required")]
    [MinLength(1, ErrorMessage = "Code cannot be empty")]
    public string Code { get; set; } = default!;

    [Required(ErrorMessage = "Language is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Language must be between 1 and 50 characters")]
    public string Language { get; set; } = default!;

    [StringLength(500, ErrorMessage = "Description must be at most 500 characters")]
    public string? Description { get; set; }

    [MaxLength(2000, ErrorMessage = "Notes must be at most 2000 characters")]
    public string? Notes { get; set; }

    public List<string> Tags { get; set; } = [];
}

public class UpdateSnippetRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
    public string Title { get; set; } = default!;

    [Required(ErrorMessage = "Code is required")]
    [MinLength(1, ErrorMessage = "Code cannot be empty")]
    public string Code { get; set; } = default!;

    [Required(ErrorMessage = "Language is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Language must be between 1 and 50 characters")]
    public string Language { get; set; } = default!;

    [StringLength(500, ErrorMessage = "Description must be at most 500 characters")]
    public string? Description { get; set; }

    [MaxLength(2000, ErrorMessage = "Notes must be at most 2000 characters")]
    public string? Notes { get; set; }

    public List<string> Tags { get; set; } = [];
}

public class SnippetListResponse
{
    public int SnippetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Language { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string AuthorName { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public int CopyCount { get; set; }
    public int FavoriteCount { get; set; }
    public bool IsFavorited { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SnippetDetailResponse
{
    public int SnippetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = [];
    public string AuthorName { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public int CopyCount { get; set; }
    public int FavoriteCount { get; set; }
    public bool IsFavorited { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SnippetListParams
{
    public string? Search { get; set; }
    public string? Language { get; set; }
    public string? Tag { get; set; }
    public string SortBy { get; set; } = "recent";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SnippetFilterOptions
{
    public List<string> Tags { get; set; } = [];
    public List<string> Languages { get; set; } = [];
}
