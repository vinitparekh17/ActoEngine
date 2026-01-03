namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

public sealed record EntityRef(
    EntityType Type,
    int Id,
    string? Name = null
)
{
    public string StableKey => $"{Type}:{Id}";

    public bool Equals(EntityRef? other)
    {
        if (other is null) return false;
        return Type == other.Type && Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Id);
    }
}
