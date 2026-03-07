using System.Text.Json;

namespace ClaudeUsageWidgetProvider;

internal sealed class HistoryRecord
{
    public string Timestamp { get; set; } = "";
    public Dictionary<string, double> Limits { get; set; } = new();
}

internal sealed class UsageHistoryStore
{
    public static readonly UsageHistoryStore Instance = new();

    private readonly Dictionary<string, List<HistoryRecord>> _cache = new();

    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = false };

    private const int MaxRecords = 2016; // 14 dní × 144 bucketů/den (10min interval)

    private UsageHistoryStore() { }

    public void Append(string? accountKey, UsageData usage)
    {
        if (string.IsNullOrEmpty(accountKey)) return;
        try
        {
            var records = GetOrLoad(accountKey);

            var ts = DateTimeOffset.UtcNow;
            var bucket = new DateTimeOffset(ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute / 10 * 10, 0, TimeSpan.Zero)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            var record = new HistoryRecord
            {
                Timestamp = bucket,
                Limits = usage.Limits.ToDictionary(l => l.Label, l => l.Utilization)
            };

            var idx = records.FindIndex(r => r.Timestamp == bucket);
            if (idx >= 0)
                records[idx] = record;
            else
                records.Add(record);

            if (records.Count > MaxRecords)
                records.RemoveRange(0, records.Count - MaxRecords);

            var json = JsonSerializer.Serialize(records, SerializerOptions);
            AtomicWrite(GetHistoryPath(accountKey), json);
        }
        catch
        {
            // Failure-silent — widget pokračuje bez persistence
        }
    }

    public IReadOnlyList<HistoryRecord> GetHistory(string accountKey)
    {
        return GetOrLoad(accountKey).AsReadOnly();
    }

    public IReadOnlyList<double> GetUtilizationHistory(string accountKey, string label)
    {
        return GetOrLoad(accountKey)
            .Where(r => r.Limits.ContainsKey(label))
            .Select(r => r.Limits[label])
            .ToList()
            .AsReadOnly();
    }

    private List<HistoryRecord> GetOrLoad(string accountKey)
    {
        if (_cache.TryGetValue(accountKey, out var cached))
            return cached;

        var path = GetHistoryPath(accountKey);
        var loaded = TryReadFromDisk(path) ?? new List<HistoryRecord>();
        _cache[accountKey] = loaded;
        return loaded;
    }

    private static List<HistoryRecord>? TryReadFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<HistoryRecord>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void AtomicWrite(string targetPath, string json)
    {
        var dir = Path.GetDirectoryName(targetPath)!;
        var tmp = Path.Combine(dir, Path.GetFileNameWithoutExtension(targetPath) + ".tmp");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(tmp, json);
            File.Move(tmp, targetPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    private static string GetHistoryPath(string accountKey)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeUsageWidget", "history");
        // Sanitizace: "claude:ORG_ID" → "claude_ORG_ID"
        var filename = accountKey.Replace(':', '_') + ".json";
        return Path.Combine(dir, filename);
    }
}
