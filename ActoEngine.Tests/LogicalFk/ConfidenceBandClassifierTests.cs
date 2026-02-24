using ActoEngine.Tests.Builders;
using ActoEngine.WebApi.Features.LogicalFk;

namespace ActoEngine.Tests.LogicalFk;

public class ConfidenceBandClassifierTests
{
    private readonly ConfidenceCalculator _calculator =
        new(ConfigBuilder.Default().Build());

    [Theory]
    [InlineData(0.50, ConfidenceBand.Low)]
    [InlineData(0.55, ConfidenceBand.Possible)]
    [InlineData(0.70, ConfidenceBand.Likely)]
    [InlineData(0.85, ConfidenceBand.VeryLikely)]
    [InlineData(0.95, ConfidenceBand.HighlyConfident)]
    public void Classify_Thresholds_AreStable(decimal confidence, ConfidenceBand expected)
    {
        var band = ConfidenceBandClassifier.Classify(confidence);
        Assert.Equal(expected, band);
    }

    [Theory]
    [MemberData(nameof(MatrixBandScenarios))]
    public void MatrixBands_MatchContract(DetectionSignals signals, ConfidenceBand expectedBand)
    {
        var result = _calculator.ComputeConfidence(signals);
        Assert.Equal(expectedBand, ConfidenceBandClassifier.Classify(result.FinalConfidence));
    }

    public static TheoryData<DetectionSignals, ConfidenceBand> MatrixBandScenarios => new()
    {
        { SignalBuilder.Create().WithNaming().WithTypeMatch().WithIdSuffix().Build(), ConfidenceBand.Likely },
        { SignalBuilder.Create().WithNaming().WithTypeMatch(false).WithIdSuffix().Build(), ConfidenceBand.Low },
        { SignalBuilder.Create().WithSpJoin(spCount: 1).WithTypeMatch().WithIdSuffix().Build(), ConfidenceBand.Likely },
        { SignalBuilder.Create().WithSpJoin(spCount: 5).WithTypeMatch().WithIdSuffix().Build(), ConfidenceBand.VeryLikely },
        { SignalBuilder.Create().WithSpJoin(spCount: 5).WithTypeMatch(false).WithIdSuffix(false).Build(), ConfidenceBand.Possible },
        { SignalBuilder.Create().WithNaming().WithSpJoin(spCount: 1).WithTypeMatch().WithIdSuffix().Build(), ConfidenceBand.HighlyConfident },
        { SignalBuilder.Create().WithNaming().WithSpJoin(spCount: 1).WithTypeMatch(false).WithIdSuffix().Build(), ConfidenceBand.Likely }
    };
}
