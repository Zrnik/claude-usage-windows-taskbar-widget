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
        // Sanitizace: "claude:ORG_ID" → "claude_ORG_ID"
        var filename = accountKey.Replace(':', '_') + ".json";
        return Path.Combine(GetHistoryDir(), filename);
    }

    private static string GetHistoryDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ClaudeUsageWidget", "history");

    private static string GetLegacyHistoryDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWidget", "history");

    public void MigrateFromAppData()
    {
        try
        {
            var oldDir = GetLegacyHistoryDir();
            if (!Directory.Exists(oldDir)) return;

            foreach (var oldPath in Directory.GetFiles(oldDir, "*.json"))
            {
                if (oldPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                var filename = Path.GetFileName(oldPath);
                var newPath = Path.Combine(GetHistoryDir(), filename);

                var oldRecords = TryReadFromDisk(oldPath) ?? new List<HistoryRecord>();
                var newRecords = TryReadFromDisk(newPath) ?? new List<HistoryRecord>();

                var merged = oldRecords
                    .Concat(newRecords)
                    .GroupBy(r => r.Timestamp)
                    .Select(g => g.Last())
                    .OrderBy(r => r.Timestamp)
                    .ToList();

                var json = JsonSerializer.Serialize(merged, SerializerOptions);
                AtomicWrite(newPath, json);

                var accountKey = Path.GetFileNameWithoutExtension(filename);
                _cache[accountKey] = merged;
            }
        }
        catch { }
    }

    // When the account key changes (e.g. token format changed from JWT to opaque),
    // copy data from the single orphaned history file to the new key's file.
    // The original file is intentionally kept — data is never deleted.
    public void MigrateOrphanedHistory(string targetKey, IReadOnlyList<string> allActiveKeys)
    {
        try
        {
            var targetPath = GetHistoryPath(targetKey);
            if (File.Exists(targetPath)) return;

            var dir = GetHistoryDir();
            if (!Directory.Exists(dir)) return;

            var activeFiles = allActiveKeys
                .Select(k => Path.GetFullPath(GetHistoryPath(k)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var orphans = Directory.GetFiles(dir, "*.json")
                .Where(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                .Where(f => !activeFiles.Contains(Path.GetFullPath(f)))
                .ToList();

            if (orphans.Count != 1) return;

            var orphanData = TryReadFromDisk(orphans[0]);
            if (orphanData == null || orphanData.Count == 0) return;

            var json = JsonSerializer.Serialize(orphanData, SerializerOptions);
            AtomicWrite(targetPath, json);
            _cache[targetKey] = orphanData;
        }
        catch { }
    }
}
