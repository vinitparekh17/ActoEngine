using ActoEngine.WebApi.Features.LogicalFk;

namespace ActoEngine.Tests.Builders;

/// <summary>
/// Fluent builder for <see cref="DetectionSignals"/>.
/// Keeps test code concise and readable.
/// </summary>
public sealed class SignalBuilder
{
    private bool _namingDetected;
    private bool _spJoinDetected;
    private bool _typeMatch;
    private bool _hasIdSuffix;
    private int _spCount;

    public static SignalBuilder Create() => new();

    public SignalBuilder WithNaming(bool detected = true)
    {
        _namingDetected = detected;
        return this;
    }

    public SignalBuilder WithSpJoin(bool detected = true, int spCount = 1)
    {
        _spJoinDetected = detected;
        _spCount = spCount;
        return this;
    }

    public SignalBuilder WithTypeMatch(bool match = true)
    {
        _typeMatch = match;
        return this;
    }

    public SignalBuilder WithIdSuffix(bool has = true)
    {
        _hasIdSuffix = has;
        return this;
    }

    public DetectionSignals Build() => new()
    {
        NamingDetected = _namingDetected,
        SpJoinDetected = _spJoinDetected,
        TypeMatch = _typeMatch,
        HasIdSuffix = _hasIdSuffix,
        SpCount = _spCount
    };
}
