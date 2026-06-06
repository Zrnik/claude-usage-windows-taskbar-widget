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
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

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
            if (idx >= 0) records[idx] = record;
            else records.Add(record);
            records.RemoveAll(r => DateTimeOffset.TryParse(r.Timestamp, out var rts) &&
                                   rts < DateTimeOffset.UtcNow.AddDays(-30));
            AtomicWrite(GetHistoryPath(accountKey), JsonSerializer.Serialize(records, SerializerOptions));
        }
        catch { }
    }

    public IReadOnlyList<HistoryRecord> GetHistory(string accountKey) => GetOrLoad(accountKey).AsReadOnly();

    public IReadOnlyList<double> GetUtilizationHistory(string accountKey, string label) =>
        GetOrLoad(accountKey).Where(r => r.Limits.ContainsKey(label)).Select(r => r.Limits[label]).ToList();

    public IReadOnlyList<(string Label, string Display)> GetKnownLabels()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!Directory.Exists(XdgPaths.HistoryDir)) return [];
            foreach (var file in Directory.GetFiles(XdgPaths.HistoryDir, "*.json"))
            {
                if (Path.GetFileName(file).StartsWith("toggl", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(file).StartsWith("jira", StringComparison.OrdinalIgnoreCase))
                    continue;
                var accountKey = Path.GetFileNameWithoutExtension(file).Replace('_', ':');
                var prefix = accountKey.StartsWith("codex", StringComparison.OrdinalIgnoreCase) ? "Codex" : "Claude";
                var records = TryReadFromDisk(file);
                if (records == null || records.Count == 0) continue;
                foreach (var label in records[^1].Limits.Keys)
                    result.TryAdd(label, $"{prefix} / {label}");
            }
        }
        catch { }
        return result.Select(kv => (kv.Key, kv.Value)).OrderBy(x => x.Value).ToList();
    }

    private List<HistoryRecord> GetOrLoad(string accountKey)
    {
        if (_cache.TryGetValue(accountKey, out var cached)) return cached;
        var loaded = TryReadFromDisk(GetHistoryPath(accountKey)) ?? [];
        _cache[accountKey] = loaded;
        return loaded;
    }

    private static List<HistoryRecord>? TryReadFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<List<HistoryRecord>>(File.ReadAllText(path));
        }
        catch { return null; }
    }

    private static void AtomicWrite(string targetPath, string json)
    {
        var dir = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, Path.GetFileNameWithoutExtension(targetPath) + ".tmp");
        File.WriteAllText(tmp, json);
        File.Move(tmp, targetPath, overwrite: true);
    }

    private static string GetHistoryPath(string accountKey)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(accountKey.Length);
        foreach (var c in accountKey)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return Path.Combine(XdgPaths.HistoryDir, sb + ".json");
    }
}
