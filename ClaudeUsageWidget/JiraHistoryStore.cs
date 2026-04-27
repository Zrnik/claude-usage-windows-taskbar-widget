using System.Text.Json;

namespace ClaudeUsageWidgetProvider;

internal sealed class JiraHistoryRecord
{
    public string Date { get; set; } = "";        // "2026-04-27" local
    public int MyDoneIssues { get; set; }
    public int MyTotalIssues { get; set; }
    public double MyDoneSp { get; set; }
    public double MyTotalSp { get; set; }
    public int MyRank { get; set; }
    public int RankingSize { get; set; }
    /// <summary>accountId → done_count snapshot (for ranking trend reconstruction).</summary>
    public Dictionary<string, int> DoneByDev { get; set; } = new();
}

internal sealed class JiraHistoryStore
{
    public static readonly JiraHistoryStore Instance = new();

    private List<JiraHistoryRecord>? _cache;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private JiraHistoryStore() { }

    public void Append(JiraUsageData usage)
    {
        if (usage.Me == null) return;
        try
        {
            var records = GetOrLoad();
            var today = DateTimeOffset.Now.ToString("yyyy-MM-dd");
            int myTotal = usage.MyByCategory.Values.Sum();
            int myDone = usage.MyByCategory.TryGetValue("done", out var d) ? d : 0;

            var doneByDev = usage.DeveloperRanking.ToDictionary(s => s.AccountId, s => s.DoneIssues);

            var record = new JiraHistoryRecord
            {
                Date = today,
                MyDoneIssues = myDone,
                MyTotalIssues = myTotal,
                MyDoneSp = usage.MyDoneStoryPoints,
                MyTotalSp = usage.MyStoryPoints,
                MyRank = usage.MyRank,
                RankingSize = usage.DeveloperRanking.Count,
                DoneByDev = doneByDev
            };

            var idx = records.FindIndex(r => r.Date == today);
            if (idx >= 0) records[idx] = record;
            else records.Add(record);

            // Keep last 400 days
            var cutoff = DateTimeOffset.Now.AddDays(-400).ToString("yyyy-MM-dd");
            records.RemoveAll(r => string.Compare(r.Date, cutoff, StringComparison.Ordinal) < 0);

            var json = JsonSerializer.Serialize(records, SerializerOptions);
            AtomicWrite(GetHistoryPath(), json);
        }
        catch { }
    }

    public IReadOnlyList<JiraHistoryRecord> GetHistory() => GetOrLoad().AsReadOnly();

    public IReadOnlyList<JiraHistoryRecord> GetLastDays(int days)
    {
        var cutoff = DateTimeOffset.Now.AddDays(-days).ToString("yyyy-MM-dd");
        return GetOrLoad()
            .Where(r => string.Compare(r.Date, cutoff, StringComparison.Ordinal) >= 0)
            .OrderBy(r => r.Date, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    private List<JiraHistoryRecord> GetOrLoad()
    {
        if (_cache != null) return _cache;
        _cache = TryReadFromDisk(GetHistoryPath()) ?? new List<JiraHistoryRecord>();
        return _cache;
    }

    private static List<JiraHistoryRecord>? TryReadFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<JiraHistoryRecord>>(json);
        }
        catch { return null; }
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
        "ClaudeUsageWidget", "history", "jira-history.json");
}
