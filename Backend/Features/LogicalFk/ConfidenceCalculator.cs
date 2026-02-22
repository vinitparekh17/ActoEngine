namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Computes graduated confidence scores using signal-based scoring with layered caps.
/// Layer 1: Strategy-based caps (SP-only, Naming-only)
/// Layer 2: Safety caps (type mismatch without corroboration)
/// Layer 3: Absolute bounds [0, 1]
/// </summary>
public class ConfidenceCalculator(DetectionConfig config)
{
    private readonly DetectionConfig _config = config;

    public ConfidenceResult ComputeConfidence(DetectionSignals signals)
    {
        // Base score — depends on the primary (highest-confidence) strategy
        decimal baseScore;
        if (signals.Corroborated)
        {
            // For corroborated, use whichever base is higher
            baseScore = Math.Max(_config.NamingBaseScore, _config.SpJoinBaseScore);
        }
        else if (signals.SpJoinDetected)
        {
            baseScore = _config.SpJoinBaseScore;
        }
        else
        {
            baseScore = _config.NamingBaseScore;
        }

        // Naming bonus (SP JOIN strategy gets a boost when column also follows naming pattern)
        decimal namingBonus =
            (signals.SpJoinDetected && signals.HasIdSuffix)
            ? _config.NamingBonus : 0m;

        // Type match bonus/penalty
        decimal typeBonus = signals.TypeMatch
            ? _config.TypeMatchBonus
            : _config.TypeMismatchPenalty;

        // Repetition bonus (SP JOIN only — finding the same join across multiple SPs)
        decimal repetitionBonus = 0m;
        if (signals.SpJoinDetected && signals.SpCount > 1)
        {
            repetitionBonus = Math.Min(
                _config.RepetitionBonusCap,
                (signals.SpCount - 1) * _config.RepetitionBonusPerSp
            );
        }

        // Corroboration bonus
        decimal corroborationBonus = signals.Corroborated
            ? _config.CorroborationBonus : 0m;

        // Sum
        decimal rawConfidence = baseScore + namingBonus + typeBonus +
                                repetitionBonus + corroborationBonus;

        // Apply layered caps
        decimal finalConfidence = ApplyLayeredCaps(rawConfidence, signals);

        // Round to 2 decimals
        finalConfidence = Math.Round(finalConfidence, 2, MidpointRounding.AwayFromZero);

        return new ConfidenceResult
        {
            BaseScore = baseScore,
            NamingBonus = namingBonus,
            TypeBonus = typeBonus,
            RepetitionBonus = repetitionBonus,
            CorroborationBonus = corroborationBonus,
            RawConfidence = rawConfidence,
            FinalConfidence = finalConfidence,
            CapsApplied = GetAppliedCaps(rawConfidence, finalConfidence, signals)
        };
    }

    private decimal ApplyLayeredCaps(decimal raw, DetectionSignals signals)
    {
        decimal confidence = raw;

        // Layer 1: Strategy-based caps
        if (signals.SpJoinDetected && !signals.NamingDetected)
        {
            confidence = Math.Min(_config.SpOnlyCap, confidence);
        }
        else if (signals.NamingDetected && !signals.SpJoinDetected)
        {
            confidence = Math.Min(_config.NamingOnlyCap, confidence);
        }

        // Layer 2: Safety caps (override strategy caps if more restrictive)
        // Corroboration overrides the type mismatch safety cap
        if (!signals.TypeMatch && !signals.Corroborated)
        {
            confidence = Math.Min(_config.TypeMismatchCap, confidence);
        }

        // Layer 3: Absolute bounds
        return Math.Clamp(confidence, 0m, 1.0m);
    }

    private string[] GetAppliedCaps(decimal raw, decimal final, DetectionSignals signals)
    {
        var caps = new List<string>();

        if (raw > final)
        {
            if (signals.SpJoinDetected && !signals.NamingDetected && raw > _config.SpOnlyCap)
            {
                caps.Add("SP_ONLY_CAP");
            }
            if (signals.NamingDetected && !signals.SpJoinDetected && raw > _config.NamingOnlyCap)
            {
                caps.Add("NAMING_ONLY_CAP");
            }
            if (!signals.TypeMatch && !signals.Corroborated && raw > _config.TypeMismatchCap)
            {
                caps.Add("TYPE_MISMATCH_CAP");
            }
        }

        return [.. caps];
    }
}
