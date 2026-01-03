using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models;

public class BatchContextRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one entity is required")]
    public List<EntityKey> Entities { get; set; } = [];
}

public class EntityKey
{
    [Required]
    public string EntityType { get; set; } = string.Empty;
    [Required]
    public int EntityId { get; set; }
}
