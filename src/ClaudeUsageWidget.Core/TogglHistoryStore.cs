using System.Text.Json;

namespace ClaudeUsageWidgetProvider;

internal sealed class TogglHistoryRecord
{
    public string Date { get; set; } = "";
    public double EarnedCzk { get; set; }
    public double Hours { get; set; }
    public double TargetCzk { get; set; }
}

internal sealed class TogglHistoryStore
{
    public static readonly TogglHistoryStore Instance = new();
    private List<TogglHistoryRecord>? _cache;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private TogglHistoryStore() { }

    public void Append(TogglUsageData usage)
    {
        try
        {
            var records = GetOrLoad();
            var today = DateTimeOffset.Now.ToString("yyyy-MM-dd");
            var idx = records.FindIndex(r => r.Date == today);
            var record = new TogglHistoryRecord
            {
                Date = today,
                EarnedCzk = usage.EarnedCzk,
                Hours = usage.HoursWorked,
                TargetCzk = usage.TargetCzk
            };
            if (idx >= 0) records[idx] = record;
            else records.Add(record);
            var cutoff = DateTimeOffset.Now.AddDays(-400).ToString("yyyy-MM-dd");
            records.RemoveAll(r => string.Compare(r.Date, cutoff, StringComparison.Ordinal) < 0);
            AtomicWrite(GetHistoryPath(), JsonSerializer.Serialize(records, SerializerOptions));
        }
        catch { }
    }

    public IReadOnlyList<TogglHistoryRecord> GetHistory() => GetOrLoad().AsReadOnly();

    public IReadOnlyList<TogglHistoryRecord> GetCurrentMonth()
    {
        var prefix = DateTimeOffset.Now.ToString("yyyy-MM");
        return GetOrLoad().Where(r => r.Date.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(r => r.Date, StringComparer.Ordinal).ToList();
    }

    public void SaveSnapshot(TogglUsageData usage)
    {
        try
        {
            AtomicWrite(GetSnapshotPath(), JsonSerializer.Serialize(usage, SerializerOptions));
        }
        catch { }
    }

    public TogglUsageData? LoadSnapshot()
    {
        try
        {
            var path = GetSnapshotPath();
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<TogglUsageData>(File.ReadAllText(path));
        }
        catch { return null; }
    }

    private List<TogglHistoryRecord> GetOrLoad()
    {
        if (_cache != null) return _cache;
        _cache = TryReadFromDisk(GetHistoryPath()) ?? [];
        return _cache;
    }

    private static List<TogglHistoryRecord>? TryReadFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<List<TogglHistoryRecord>>(File.ReadAllText(path));
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

    private static string GetHistoryPath() => Path.Combine(XdgPaths.HistoryDir, "toggl-history.json");
    private static string GetSnapshotPath() => Path.Combine(XdgPaths.HistoryDir, "toggl-snapshot.json");
}
