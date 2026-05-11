using Microsoft.Win32;

namespace ClaudeUsageWidgetProvider;

internal sealed class SettingsStore
{
    public static readonly SettingsStore Instance = new();

    private const string RegistryPath = @"Software\ClaudeUsageWidget";
    private const string ChartWindowsSubKey = @"Software\ClaudeUsageWidget\ChartWindows";
    private const string TogglRatesSubKey = @"Software\ClaudeUsageWidget\TogglRates";
    private const string HiddenLimitsValueName = "HiddenLimits"; // comma-separated label list

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

    public static event Action? VisibilityChanged;
    public static void RaiseVisibilityChanged() => VisibilityChanged?.Invoke();

    public static event Action? IncognitoChanged;
    public static void RaiseIncognitoChanged() => IncognitoChanged?.Invoke();

    public static event Action? TogglRefreshRequested;
    public static void RaiseTogglRefreshRequested() => TogglRefreshRequested?.Invoke();

    public static event Action? JiraRefreshRequested;
    public static void RaiseJiraRefreshRequested() => JiraRefreshRequested?.Invoke();

    // label → hours override (e.g. "unified-5h" → 48)
    public Dictionary<string, double> ChartWindowHours { get; private set; } = new();

    // Labely jednotlivých rate-limit barů, které uživatel skryl (e.g. "GPT-5.3-Codex-Spark-7d")
    public HashSet<string> HiddenLimits { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsLimitHidden(string label) => HiddenLimits.Contains(label);

    public string TogglApiKey { get; set; } = "";
    public double TogglMonthlyTargetCzk { get; set; }
    // project_id → CZK/hour
    public Dictionary<long, double> TogglProjectRates { get; private set; } = new();

    // Workday window for fractional "today remaining" calc (24h decimal hours)
    public double WorkdayStartHour { get; set; } = 9.0;
    public double WorkdayEndHour { get; set; } = 17.0;

    // JIRA integration
    public string JiraUrl { get; set; } = "";
    public string JiraEmail { get; set; } = "";
    public string JiraApiToken { get; set; } = "";
    public string JiraProjectKey { get; set; } = "";
    // accountId → include in comparison ("developers")
    public HashSet<string> JiraDeveloperAccountIds { get; private set; } = new();

    private SettingsStore()
    {
        Load();
    }

    private void Load()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key == null) return;
            NotificationsEnabled = (int)(key.GetValue("NotificationsEnabled", 0) ?? 0) != 0;
            NotifyOnReset = (int)(key.GetValue("NotifyOnReset", 0) ?? 0) != 0;
            IncognitoMode = (int)(key.GetValue("IncognitoMode", 0) ?? 0) != 0;
            ShowClaude = (int)(key.GetValue("ShowClaude", 1) ?? 1) != 0;
            ShowCodex = (int)(key.GetValue("ShowCodex", 1) ?? 1) != 0;
            ShowToggl = (int)(key.GetValue("ShowToggl", 1) ?? 1) != 0;
            ShowJira = (int)(key.GetValue("ShowJira", 1) ?? 1) != 0;
            ClaudeWidth = ParseWidth(key.GetValue("ClaudeWidth"), DefaultTileWidth);
            CodexWidth = ParseWidth(key.GetValue("CodexWidth"), DefaultTileWidth);
            TogglWidth = ParseWidth(key.GetValue("TogglWidth"), DefaultTileWidth);
            JiraWidth = ParseWidth(key.GetValue("JiraWidth"), DefaultTileWidth);
            TogglApiKey = key.GetValue("TogglApiKey") as string ?? "";
            var targetRaw = key.GetValue("TogglMonthlyTargetCzk") as string;
            if (!string.IsNullOrEmpty(targetRaw) &&
                double.TryParse(targetRaw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var target))
                TogglMonthlyTargetCzk = target;
            var workdayStartRaw = key.GetValue("WorkdayStartHour") as string;
            if (!string.IsNullOrEmpty(workdayStartRaw) &&
                double.TryParse(workdayStartRaw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var ws) && ws is >= 0 and <= 24)
                WorkdayStartHour = ws;
            var workdayEndRaw = key.GetValue("WorkdayEndHour") as string;
            if (!string.IsNullOrEmpty(workdayEndRaw) &&
                double.TryParse(workdayEndRaw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var we) && we is >= 0 and <= 24)
                WorkdayEndHour = we;
            JiraUrl = key.GetValue("JiraUrl") as string ?? "";
            JiraEmail = key.GetValue("JiraEmail") as string ?? "";
            JiraApiToken = key.GetValue("JiraApiToken") as string ?? "";
            JiraProjectKey = key.GetValue("JiraProjectKey") as string ?? "";
            var devsRaw = key.GetValue("JiraDeveloperAccountIds") as string;
            if (!string.IsNullOrEmpty(devsRaw))
                foreach (var id in devsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    JiraDeveloperAccountIds.Add(id.Trim());
            var hiddenRaw = key.GetValue(HiddenLimitsValueName) as string;
            if (!string.IsNullOrEmpty(hiddenRaw))
                foreach (var lbl in hiddenRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    HiddenLimits.Add(lbl.Trim());
        }
        catch { }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ChartWindowsSubKey);
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var raw = key.GetValue(name);
                    double hours = 0;
                    if (raw is int intVal) hours = intVal; // legacy: days stored as DWord
                    else if (raw is string str) double.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, out hours);
                    if (hours > 0) ChartWindowHours[name] = hours;
                }
            }
        }
        catch { }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TogglRatesSubKey);
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    if (!long.TryParse(name, out var projectId)) continue;
                    var raw = key.GetValue(name) as string;
                    if (raw != null && double.TryParse(raw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var rate) && rate > 0)
                        TogglProjectRates[projectId] = rate;
                }
            }
        }
        catch { }
    }

    public void Save()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key.SetValue("NotificationsEnabled", NotificationsEnabled ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("NotifyOnReset", NotifyOnReset ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("IncognitoMode", IncognitoMode ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowClaude", ShowClaude ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowCodex", ShowCodex ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowToggl", ShowToggl ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowJira", ShowJira ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ClaudeWidth", ClaudeWidth.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
            key.SetValue("CodexWidth", CodexWidth.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
            key.SetValue("TogglWidth", TogglWidth.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
            key.SetValue("JiraWidth", JiraWidth.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
            key.SetValue("TogglApiKey", TogglApiKey, RegistryValueKind.String);
            key.SetValue("TogglMonthlyTargetCzk",
                TogglMonthlyTargetCzk.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RegistryValueKind.String);
            key.SetValue("WorkdayStartHour",
                WorkdayStartHour.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RegistryValueKind.String);
            key.SetValue("WorkdayEndHour",
                WorkdayEndHour.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RegistryValueKind.String);
            key.SetValue("JiraUrl", JiraUrl, RegistryValueKind.String);
            key.SetValue("JiraEmail", JiraEmail, RegistryValueKind.String);
            key.SetValue("JiraApiToken", JiraApiToken, RegistryValueKind.String);
            key.SetValue("JiraProjectKey", JiraProjectKey, RegistryValueKind.String);
            key.SetValue("JiraDeveloperAccountIds", string.Join(",", JiraDeveloperAccountIds), RegistryValueKind.String);
            key.SetValue(HiddenLimitsValueName, string.Join(",", HiddenLimits), RegistryValueKind.String);
        }
        catch { }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ChartWindowsSubKey);
            foreach (var (label, hours) in ChartWindowHours)
                key.SetValue(label, hours.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
        }
        catch { }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(TogglRatesSubKey);
            // Remove stale values not in current dict
            foreach (var name in key.GetValueNames())
                if (!long.TryParse(name, out var id) || !TogglProjectRates.ContainsKey(id))
                    key.DeleteValue(name, throwOnMissingValue: false);
            foreach (var (projectId, rate) in TogglProjectRates)
                key.SetValue(projectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    rate.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    RegistryValueKind.String);
        }
        catch { }
    }

    /// <summary>
    /// Returns known labels with display prefix, e.g. ("unified-5h", "Claude / unified-5h")
    /// </summary>
    private static double ParseWidth(object? raw, double fallback)
    {
        if (raw is string s &&
            double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var w) && w >= 50 && w <= 600)
            return w;
        return fallback;
    }

    public IReadOnlyList<(string Label, string Display)> GetKnownLabels()
    {
        var result = new Dictionary<string, string>();

        // From registry overrides
        foreach (var label in ChartWindowHours.Keys)
            result.TryAdd(label, label);

        // From history files
        try
        {
            var historyDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClaudeUsageWidget", "history");
            if (Directory.Exists(historyDir))
            {
                foreach (var file in Directory.GetFiles(historyDir, "*.json"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var prefix = fileName.StartsWith("codex", StringComparison.OrdinalIgnoreCase)
                        ? "Codex" : "Claude";
                    var accountKey = fileName.Replace('_', ':');
                    var history = UsageHistoryStore.Instance.GetHistory(accountKey);
                    if (history.Count > 0)
                    {
                        foreach (var label in history[^1].Limits.Keys)
                            result.TryAdd(label, $"{prefix} / {label}");
                    }
                }
            }
        }
        catch { }

        return result.Select(kv => (kv.Key, kv.Value)).OrderBy(x => x.Value).ToList();
    }
}
