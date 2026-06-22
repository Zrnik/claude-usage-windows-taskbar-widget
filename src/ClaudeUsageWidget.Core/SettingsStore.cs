using System.Text.Json;

namespace ClaudeUsageWidgetProvider;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static readonly SettingsStore Instance = new();

    public bool NotificationsEnabled { get; set; }
    public bool NotifyOnReset { get; set; }
    public bool IncognitoMode { get; set; }

    public bool ShowClaude { get; set; } = true;
    public bool ShowCodex { get; set; } = true;
    public bool ShowToggl { get; set; } = true;
    public bool ShowJira { get; set; } = true;

    public const double DefaultTileWidth = 170;
    public double ClaudeWidth { get; set; } = DefaultTileWidth;
    public double CodexWidth { get; set; } = DefaultTileWidth;
    public double TogglWidth { get; set; } = DefaultTileWidth;
    public double JiraWidth { get; set; } = DefaultTileWidth;

    public Dictionary<string, double> ChartWindowHours { get; private set; } = new();
    public HashSet<string> HiddenLimits { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public string TogglApiKey { get; set; } = "";
    public double TogglMonthlyTargetCzk { get; set; }
    public Dictionary<long, double> TogglProjectRates { get; private set; } = new();
    public double WorkdayStartHour { get; set; } = 9.0;
    public double WorkdayEndHour { get; set; } = 17.0;

    public string JiraUrl { get; set; } = "";
    public string JiraEmail { get; set; } = "";
    public string JiraApiToken { get; set; } = "";
    public string JiraProjectKey { get; set; } = "";
    public HashSet<string> JiraDeveloperAccountIds { get; private set; } = new();

    private readonly Dictionary<string, string> _runtimeKnownLabels = new(StringComparer.OrdinalIgnoreCase);

    public static event Action? VisibilityChanged;
    public static void RaiseVisibilityChanged() => VisibilityChanged?.Invoke();
    public static event Action? IncognitoChanged;
    public static void RaiseIncognitoChanged() => IncognitoChanged?.Invoke();
    public static event Action? KnownLabelsChanged;

    private SettingsStore()
    {
        Load();
    }

    public bool IsLimitHidden(string label) => HiddenLimits.Contains(label);

    public void RegisterKnownLabels(ServiceType service, UsageData usage)
    {
        var prefix = service == ServiceType.Codex ? "Codex" : "Claude";
        var changed = false;
        foreach (var limit in usage.Limits)
        {
            var display = $"{prefix} / {limit.Label}";
            if (_runtimeKnownLabels.TryGetValue(limit.Label, out var current) && current == display)
                continue;
            _runtimeKnownLabels[limit.Label] = display;
            changed = true;
        }
        if (changed) KnownLabelsChanged?.Invoke();
    }

    public IReadOnlyList<(string Label, string Display)> GetKnownLabels()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, display) in _runtimeKnownLabels)
            result.TryAdd(label, display);
        foreach (var label in ChartWindowHours.Keys)
            result.TryAdd(label, label);
        foreach (var (label, display) in UsageHistoryStore.Instance.GetKnownLabels())
            result.TryAdd(label, display);
        return result.Select(kv => (kv.Key, kv.Value)).OrderBy(x => x.Value).ToList();
    }

    public SettingsSnapshot ToSnapshot(bool includeSecrets = true) => new()
    {
        NotificationsEnabled = NotificationsEnabled,
        NotifyOnReset = NotifyOnReset,
        IncognitoMode = IncognitoMode,
        ShowClaude = ShowClaude,
        ShowCodex = ShowCodex,
        ShowToggl = ShowToggl,
        ShowJira = ShowJira,
        ClaudeWidth = ClaudeWidth,
        CodexWidth = CodexWidth,
        TogglWidth = TogglWidth,
        JiraWidth = JiraWidth,
        ChartWindowHours = new(ChartWindowHours),
        HiddenLimits = HiddenLimits.ToList(),
        TogglApiKey = includeSecrets ? TogglApiKey : "",
        TogglApiKeyConfigured = !string.IsNullOrWhiteSpace(TogglApiKey),
        TogglMonthlyTargetCzk = TogglMonthlyTargetCzk,
        TogglProjectRates = new(TogglProjectRates),
        WorkdayStartHour = WorkdayStartHour,
        WorkdayEndHour = WorkdayEndHour,
        JiraUrl = JiraUrl,
        JiraEmail = JiraEmail,
        JiraApiToken = includeSecrets ? JiraApiToken : "",
        JiraApiTokenConfigured = !string.IsNullOrWhiteSpace(JiraApiToken),
        JiraProjectKey = JiraProjectKey,
        JiraDeveloperAccountIds = JiraDeveloperAccountIds.ToList()
    };

    public void Apply(SettingsSnapshot snapshot)
    {
        var incognitoChanged = IncognitoMode != snapshot.IncognitoMode;

        NotificationsEnabled = snapshot.NotificationsEnabled;
        NotifyOnReset = snapshot.NotifyOnReset;
        IncognitoMode = snapshot.IncognitoMode;
        ShowClaude = snapshot.ShowClaude;
        ShowCodex = snapshot.ShowCodex;
        ShowToggl = snapshot.ShowToggl;
        ShowJira = snapshot.ShowJira;
        ClaudeWidth = ClampWidth(snapshot.ClaudeWidth, ClaudeWidth);
        CodexWidth = ClampWidth(snapshot.CodexWidth, CodexWidth);
        TogglWidth = ClampWidth(snapshot.TogglWidth, TogglWidth);
        JiraWidth = ClampWidth(snapshot.JiraWidth, JiraWidth);
        ChartWindowHours = snapshot.ChartWindowHours.Where(kv => kv.Value > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        HiddenLimits = new HashSet<string>(snapshot.HiddenLimits, StringComparer.OrdinalIgnoreCase);
        var togglApiKey = snapshot.TogglApiKey.Trim();
        if (!string.IsNullOrEmpty(togglApiKey))
            TogglApiKey = togglApiKey;
        TogglMonthlyTargetCzk = Math.Max(0, snapshot.TogglMonthlyTargetCzk);
        TogglProjectRates = snapshot.TogglProjectRates.Where(kv => kv.Value > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        WorkdayStartHour = ClampHour(snapshot.WorkdayStartHour, WorkdayStartHour);
        WorkdayEndHour = ClampHour(snapshot.WorkdayEndHour, WorkdayEndHour);
        JiraUrl = NormalizeJiraUrl(snapshot.JiraUrl);
        JiraEmail = snapshot.JiraEmail.Trim();
        var jiraApiToken = snapshot.JiraApiToken.Trim();
        if (!string.IsNullOrEmpty(jiraApiToken))
            JiraApiToken = jiraApiToken;
        JiraProjectKey = snapshot.JiraProjectKey.Trim();
        JiraDeveloperAccountIds = new HashSet<string>(snapshot.JiraDeveloperAccountIds);

        Save();
        VisibilityChanged?.Invoke();
        if (incognitoChanged) IncognitoChanged?.Invoke();
    }

    public void Save()
    {
        try
        {
            var path = XdgPaths.SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(ToSnapshot(), JsonOptions);
            File.WriteAllText(path, json);
            TryChmod600(path);
            Console.Error.WriteLine($"Settings saved to {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save settings to {SafeSettingsPath()}: {ex}");
        }
    }

    private void Load()
    {
        try
        {
            var path = XdgPaths.SettingsPath;
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Settings file not found at {path}; using defaults");
                return;
            }
            var snapshot = JsonSerializer.Deserialize<SettingsSnapshot>(File.ReadAllText(path), JsonOptions);
            if (snapshot != null) ApplyLoaded(snapshot);
            Console.Error.WriteLine($"Settings loaded from {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load settings from {SafeSettingsPath()}: {ex}");
        }
    }

    private void ApplyLoaded(SettingsSnapshot snapshot)
    {
        NotificationsEnabled = snapshot.NotificationsEnabled;
        NotifyOnReset = snapshot.NotifyOnReset;
        IncognitoMode = snapshot.IncognitoMode;
        ShowClaude = snapshot.ShowClaude;
        ShowCodex = snapshot.ShowCodex;
        ShowToggl = snapshot.ShowToggl;
        ShowJira = snapshot.ShowJira;
        ClaudeWidth = ClampWidth(snapshot.ClaudeWidth, DefaultTileWidth);
        CodexWidth = ClampWidth(snapshot.CodexWidth, DefaultTileWidth);
        TogglWidth = ClampWidth(snapshot.TogglWidth, DefaultTileWidth);
        JiraWidth = ClampWidth(snapshot.JiraWidth, DefaultTileWidth);
        ChartWindowHours = snapshot.ChartWindowHours ?? new();
        HiddenLimits = new HashSet<string>(snapshot.HiddenLimits ?? [], StringComparer.OrdinalIgnoreCase);
        TogglApiKey = snapshot.TogglApiKey ?? "";
        TogglMonthlyTargetCzk = Math.Max(0, snapshot.TogglMonthlyTargetCzk);
        TogglProjectRates = snapshot.TogglProjectRates ?? new();
        WorkdayStartHour = ClampHour(snapshot.WorkdayStartHour, 9.0);
        WorkdayEndHour = ClampHour(snapshot.WorkdayEndHour, 17.0);
        JiraUrl = NormalizeJiraUrl(snapshot.JiraUrl ?? "");
        JiraEmail = snapshot.JiraEmail ?? "";
        JiraApiToken = snapshot.JiraApiToken ?? "";
        JiraProjectKey = snapshot.JiraProjectKey ?? "";
        JiraDeveloperAccountIds = new HashSet<string>(snapshot.JiraDeveloperAccountIds ?? []);
    }

    private static double ClampWidth(double width, double fallback) => width is >= 50 and <= 600 ? width : fallback;
    private static double ClampHour(double hour, double fallback) => hour is >= 0 and <= 24 ? hour : fallback;

    private static string NormalizeJiraUrl(string raw)
    {
        var s = raw.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(s)) return "";
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            s = "https://" + s;
        return s;
    }

    private static void TryChmod600(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { }
    }

    private static string SafeSettingsPath()
    {
        try
        {
            return XdgPaths.SettingsPath;
        }
        catch (Exception ex)
        {
            return $"<unresolved: {ex.Message}>";
        }
    }

}

internal sealed class SettingsSnapshot
{
    public bool NotificationsEnabled { get; set; }
    public bool NotifyOnReset { get; set; }
    public bool IncognitoMode { get; set; }
    public bool ShowClaude { get; set; } = true;
    public bool ShowCodex { get; set; } = true;
    public bool ShowToggl { get; set; } = true;
    public bool ShowJira { get; set; } = true;
    public double ClaudeWidth { get; set; } = SettingsStore.DefaultTileWidth;
    public double CodexWidth { get; set; } = SettingsStore.DefaultTileWidth;
    public double TogglWidth { get; set; } = SettingsStore.DefaultTileWidth;
    public double JiraWidth { get; set; } = SettingsStore.DefaultTileWidth;
    public Dictionary<string, double> ChartWindowHours { get; set; } = new();
    public List<string> HiddenLimits { get; set; } = [];
    public string TogglApiKey { get; set; } = "";
    public bool TogglApiKeyConfigured { get; set; }
    public double TogglMonthlyTargetCzk { get; set; }
    public Dictionary<long, double> TogglProjectRates { get; set; } = new();
    public double WorkdayStartHour { get; set; } = 9.0;
    public double WorkdayEndHour { get; set; } = 17.0;
    public string JiraUrl { get; set; } = "";
    public string JiraEmail { get; set; } = "";
    public string JiraApiToken { get; set; } = "";
    public bool JiraApiTokenConfigured { get; set; }
    public string JiraProjectKey { get; set; } = "";
    public List<string> JiraDeveloperAccountIds { get; set; } = [];
}
