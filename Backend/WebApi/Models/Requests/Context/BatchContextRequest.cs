using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models.Requests.Context;

public class BatchContextRequest
{
    [Required]
    public List<EntityKey> Entities { get; set; } = null!;
}

public class EntityKey
{
    [Required]
    public string EntityType { get; set; } = string.Empty;
    [Required]
    public int EntityId { get; set; }
}

