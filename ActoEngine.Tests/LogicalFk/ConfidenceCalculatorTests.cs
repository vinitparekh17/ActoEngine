using ActoEngine.Tests.Builders;
using ActoEngine.WebApi.Features.LogicalFk;

namespace ActoEngine.Tests.LogicalFk;

public class ConfidenceCalculatorTests
{
    private readonly ConfidenceCalculator _calculator;

    public ConfidenceCalculatorTests()
    {
        _calculator = new ConfidenceCalculator(ConfigBuilder.Default().Build());
    }

    [Fact]
    public void T01_NamingOnly_TypeMatch_Returns070_NoCaps()
    {
        var signals = SignalBuilder.Create()
            .WithNaming()
            .WithTypeMatch()
            .WithIdSuffix()
            .Build();

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.70m, result.RawConfidence);
        Assert.Equal(0.70m, result.FinalConfidence);
        Assert.Equal([], result.CapsApplied);
    }

    [Fact]
    public void T02_NamingOnly_TypeMismatch_Returns050_NoCaps()
    {
        var signals = SignalBuilder.Create()
            .WithNaming()
            .WithTypeMatch(false)
            .WithIdSuffix()
            .Build();

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.50m, result.RawConfidence);
        Assert.Equal(0.50m, result.FinalConfidence);
        Assert.Equal([], result.CapsApplied);
    }

    [Fact]
    public void T03_SpJoinSingle_TypeMatch_Returns075_NoCaps()
    {
        var signals = SignalBuilder.Create()
            .WithSpJoin(spCount: 1)
            .WithTypeMatch()
            .WithIdSuffix()
            .Build();

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.75m, result.RawConfidence);
        Assert.Equal(0.75m, result.FinalConfidence);
        Assert.Equal([], result.CapsApplied);
    }

    [Fact]
    public void T04_SpJoin5_IdSuffixOnly_TypeMatch_Returns085_WithSpOnlyCapApplied()
    {
        var signals = SignalBuilder.Create()
            .WithSpJoin(spCount: 5)
            .WithTypeMatch()
            .WithIdSuffix()
            .Build();

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.95m, result.RawConfidence);
        Assert.Equal(0.85m, result.FinalConfidence);
        Assert.Equal(["SP_ONLY_CAP"], result.CapsApplied);
    }

    [Fact]
    public void T05_SpJoin5_TypeMismatch_NoNaming_Returns055_WithTypeMismatchCapOnly()
    {
        var signals = SignalBuilder.Create()
            .WithSpJoin(spCount: 5)
            .WithTypeMatch(false)
            .WithIdSuffix(false)
            .Build();

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.60m, result.RawConfidence);
        Assert.Equal(0.55m, result.FinalConfidence);
        Assert.Equal(["TYPE_MISMATCH_CAP"], result.CapsApplied);
    }

    [Fact]
    public void T06_Corroborated_TypeMatch_Returns095_NoCaps()
    {
        var signals = SignalBuilder.Create()
            .WithNaming()
            .WithSpJoin(spCount: 1)
            .WithTypeMatch()
            .WithIdSuffix()
            .Build();

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.95m, result.RawConfidence);
        Assert.Equal(0.95m, result.FinalConfidence);
        Assert.Empty(result.CapsApplied);
    }

    [Fact]
    public void T07_Corroborated_TypeMismatch_Returns075_NoCaps()
    {
        var signals = SignalBuilder.Create()
            .WithNaming()
            .WithSpJoin(spCount: 1)
            .WithTypeMatch(false)
            .WithIdSuffix()
            .Build();

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.75m, result.RawConfidence);
        Assert.Equal(0.75m, result.FinalConfidence);
        Assert.Empty(result.CapsApplied);
    }

    [Fact]
    public void RepetitionBonus_IsCappedAtConfiguredLimit()
    {
        var signals = SignalBuilder.Create()
            .WithSpJoin(spCount: 100)
            .WithTypeMatch()
            .WithIdSuffix()
            .Build();

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.20m, result.RepetitionBonus);
    }

    [Fact]
    public void NamingBonus_NotApplied_WhenCorroborated()
    {
        var signals = SignalBuilder.Create()
            .WithNaming()
            .WithSpJoin(spCount: 1)
            .WithIdSuffix()
            .WithTypeMatch()
            .Build();

        Assert.True(signals.Corroborated);

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.00m, result.NamingBonus);
    }

    [Fact]
    public void TypeMismatchCap_IsBypassed_WhenCorroborated()
    {
        var signals = SignalBuilder.Create()
            .WithNaming()
            .WithSpJoin(spCount: 5)
            .WithTypeMatch(false)
            .WithIdSuffix()
            .Build();

        Assert.True(signals.Corroborated);

        var result = _calculator.ComputeConfidence(signals);

        Assert.Equal(0.95m, result.FinalConfidence);
        Assert.DoesNotContain("TYPE_MISMATCH_CAP", result.CapsApplied);
    }

    [Fact]
    public void FinalConfidence_IsRoundedToTwoDecimals()
    {
        var config = ConfigBuilder.Default()
            .WithNamingBaseScore(0.603m)
            .WithTypeMatchBonus(0.109m)
            .Build();

        var calculator = new ConfidenceCalculator(config);
        var signals = SignalBuilder.Create().WithNaming().WithTypeMatch().Build();

        var result = calculator.ComputeConfidence(signals);

        Assert.Equal(0.71m, result.FinalConfidence);
    }
}
