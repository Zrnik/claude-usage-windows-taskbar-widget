namespace ClaudeUsageWidgetProvider;

internal sealed class PredictionResult
{
    public enum PredictionKind { Safe, Approaching, LimitReached }

    public PredictionKind Kind { get; init; }
    public TimeSpan? TimeToLimit { get; init; }

    public string Format()
    {
        return Kind switch
        {
            PredictionKind.LimitReached => "Limit reached",
            PredictionKind.Safe => "Pace: safe",
            PredictionKind.Approaching when TimeToLimit.HasValue =>
                $"At current pace, limit in ~{FormatTimeSpan(TimeToLimit.Value)}",
            _ => "Pace: safe"
        };
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{Math.Max(1, ts.Minutes)}m";
    }
}

internal static class UsagePrediction
{
    public static PredictionResult? Predict(string? accountKey, RateLimitEntry limit)
    {
        if (string.IsNullOrEmpty(accountKey)) return null;

        if (limit.Utilization >= 100)
            return new PredictionResult { Kind = PredictionResult.PredictionKind.LimitReached };

        var history = UsageHistoryStore.Instance.GetUtilizationHistory(accountKey, limit.Label);
        if (history.Count < 2) return null;

        // Use last 12 data points (2h at 10-min intervals)
        var recent = history.Count <= 12 ? history : history.Skip(history.Count - 12).ToList();

        var first = recent[0];
        var last = recent[^1];
        var slope = (last - first) / (recent.Count - 1); // utilization change per 10-min bucket

        if (slope <= 0)
            return new PredictionResult { Kind = PredictionResult.PredictionKind.Safe };

        var bucketsTo100 = (100.0 - last) / slope;
        var timeToLimit = TimeSpan.FromMinutes(bucketsTo100 * 10);

        // If limit resets before we'd hit 100%, it's safe
        var timeToReset = limit.ResetsAt - DateTimeOffset.UtcNow;
        if (timeToReset > TimeSpan.Zero && timeToLimit > timeToReset)
            return new PredictionResult { Kind = PredictionResult.PredictionKind.Safe };

        return new PredictionResult
        {
            Kind = PredictionResult.PredictionKind.Approaching,
            TimeToLimit = timeToLimit
        };
    }
}
