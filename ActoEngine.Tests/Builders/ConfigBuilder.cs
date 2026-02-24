using ActoEngine.WebApi.Features.LogicalFk;

namespace ActoEngine.Tests.Builders;

/// <summary>
/// Fluent builder for <see cref="DetectionConfig"/>.
/// Defaults match production values. Override individual values for edge-case tests.
/// </summary>
public sealed class ConfigBuilder
{
    private readonly DetectionConfig _config = new(); // defaults = production values

    public static ConfigBuilder Default() => new();

    public ConfigBuilder WithNamingOnlyCap(decimal cap) { _config.NamingOnlyCap = cap; return this; }
    public ConfigBuilder WithSpOnlyCap(decimal cap) { _config.SpOnlyCap = cap; return this; }
    public ConfigBuilder WithTypeMismatchCap(decimal cap) { _config.TypeMismatchCap = cap; return this; }
    public ConfigBuilder WithNamingBaseScore(decimal score) { _config.NamingBaseScore = score; return this; }
    public ConfigBuilder WithSpJoinBaseScore(decimal score) { _config.SpJoinBaseScore = score; return this; }
    public ConfigBuilder WithTypeMatchBonus(decimal bonus) { _config.TypeMatchBonus = bonus; return this; }
    public ConfigBuilder WithTypeMismatchPenalty(decimal penalty) { _config.TypeMismatchPenalty = penalty; return this; }
    public ConfigBuilder WithRepetitionBonusPerSp(decimal bonus) { _config.RepetitionBonusPerSp = bonus; return this; }
    public ConfigBuilder WithRepetitionBonusCap(decimal cap) { _config.RepetitionBonusCap = cap; return this; }
    public ConfigBuilder WithCorroborationBonus(decimal bonus) { _config.CorroborationBonus = bonus; return this; }

    public DetectionConfig Build() => new()
    {
        NamingBaseScore = _config.NamingBaseScore,
        SpJoinBaseScore = _config.SpJoinBaseScore,
        NamingBonus = _config.NamingBonus,
        TypeMatchBonus = _config.TypeMatchBonus,
        TypeMismatchPenalty = _config.TypeMismatchPenalty,
        RepetitionBonusPerSp = _config.RepetitionBonusPerSp,
        RepetitionBonusCap = _config.RepetitionBonusCap,
        CorroborationBonus = _config.CorroborationBonus,
        SpOnlyCap = _config.SpOnlyCap,
        NamingOnlyCap = _config.NamingOnlyCap,
        TypeMismatchCap = _config.TypeMismatchCap
    };
}
