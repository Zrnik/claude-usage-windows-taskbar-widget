using System.Text.Json;

namespace ClaudeUsageWidgetProvider;

internal sealed class JiraHistoryRecord
{
    public string Date { get; set; } = "";
    public int MyTotalIssues { get; set; }
    public int MyDoneIssues { get; set; }
    public double MyStoryPoints { get; set; }
    public double MyDoneStoryPoints { get; set; }
    public int MyRank { get; set; }
    public int RankingSize { get; set; }
}

internal sealed class JiraHistoryStore
{
    public static readonly JiraHistoryStore Instance = new();
    private List<JiraHistoryRecord>? _cache;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private JiraHistoryStore() { }

    public void Append(JiraUsageData usage)
    {
        try
        {
            var records = GetOrLoad();
            var today = DateTimeOffset.Now.ToString("yyyy-MM-dd");
            var record = new JiraHistoryRecord
            {
                Date = today,
                MyTotalIssues = usage.MyByCategory.Values.Sum(),
                MyDoneIssues = usage.MyByCategory.TryGetValue("done", out var done) ? done : 0,
                MyStoryPoints = usage.MyStoryPoints,
                MyDoneStoryPoints = usage.MyDoneStoryPoints,
                MyRank = usage.MyRank,
                RankingSize = usage.DeveloperRanking.Count
            };
            var idx = records.FindIndex(r => r.Date == today);
            if (idx >= 0) records[idx] = record;
            else records.Add(record);
            var cutoff = DateTimeOffset.Now.AddDays(-400).ToString("yyyy-MM-dd");
            records.RemoveAll(r => string.Compare(r.Date, cutoff, StringComparison.Ordinal) < 0);
            AtomicWrite(GetHistoryPath(), JsonSerializer.Serialize(records, SerializerOptions));
        }
        catch { }
    }

    public IReadOnlyList<JiraHistoryRecord> GetLastDays(int days)
    {
        var cutoff = DateTimeOffset.Now.AddDays(-days).ToString("yyyy-MM-dd");
        return GetOrLoad().Where(r => string.Compare(r.Date, cutoff, StringComparison.Ordinal) >= 0)
            .OrderBy(r => r.Date, StringComparer.Ordinal).ToList();
    }

    private List<JiraHistoryRecord> GetOrLoad()
    {
        if (_cache != null) return _cache;
        _cache = TryReadFromDisk(GetHistoryPath()) ?? [];
        return _cache;
    }

    private static List<JiraHistoryRecord>? TryReadFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<List<JiraHistoryRecord>>(File.ReadAllText(path));
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

    private static string GetHistoryPath() => Path.Combine(XdgPaths.HistoryDir, "jira-history.json");
}
