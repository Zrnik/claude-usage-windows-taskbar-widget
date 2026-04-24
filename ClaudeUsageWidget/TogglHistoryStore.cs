using System.Text.Json;

namespace ClaudeUsageWidgetProvider;

internal sealed class TogglHistoryRecord
{
    public string Date { get; set; } = "";        // "2026-04-24" (local date)
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

            var record = new TogglHistoryRecord
            {
                Date = today,
                EarnedCzk = usage.EarnedCzk,
                Hours = usage.HoursWorked,
                TargetCzk = usage.TargetCzk
            };

            var idx = records.FindIndex(r => r.Date == today);
            if (idx >= 0)
                records[idx] = record;
            else
                records.Add(record);

            // Keep last 400 days (~13 months) — prune older
            var cutoff = DateTimeOffset.Now.AddDays(-400).ToString("yyyy-MM-dd");
            records.RemoveAll(r => string.Compare(r.Date, cutoff, StringComparison.Ordinal) < 0);

            var json = JsonSerializer.Serialize(records, SerializerOptions);
            AtomicWrite(GetHistoryPath(), json);
        }
        catch
        {
            // silent — persistence is best-effort
        }
    }

    public IReadOnlyList<TogglHistoryRecord> GetHistory() => GetOrLoad().AsReadOnly();

    public IReadOnlyList<TogglHistoryRecord> GetCurrentMonth()
    {
        var now = DateTimeOffset.Now;
        var prefix = now.ToString("yyyy-MM");
        return GetOrLoad()
            .Where(r => r.Date.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(r => r.Date, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    private List<TogglHistoryRecord> GetOrLoad()
    {
        if (_cache != null) return _cache;
        _cache = TryReadFromDisk(GetHistoryPath()) ?? new List<TogglHistoryRecord>();
        return _cache;
    }

    private static List<TogglHistoryRecord>? TryReadFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<TogglHistoryRecord>>(json);
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

    private static string GetHistoryPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ClaudeUsageWidget", "history", "toggl-history.json");
}
