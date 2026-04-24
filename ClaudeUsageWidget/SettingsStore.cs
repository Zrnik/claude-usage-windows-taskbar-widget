using Microsoft.Win32;

namespace ClaudeUsageWidgetProvider;

internal sealed class SettingsStore
{
    public static readonly SettingsStore Instance = new();

    private const string RegistryPath = @"Software\ClaudeUsageWidget";
    private const string ChartWindowsSubKey = @"Software\ClaudeUsageWidget\ChartWindows";
    private const string TogglRatesSubKey = @"Software\ClaudeUsageWidget\TogglRates";

    public bool NotificationsEnabled { get; set; }
    public bool NotifyOnReset { get; set; }
    public bool AlwaysOnTop { get; set; } = true;

    public bool ShowClaude { get; set; } = true;
    public bool ShowCodex { get; set; } = true;
    public bool ShowToggl { get; set; } = true;

    public static event Action? VisibilityChanged;
    public static void RaiseVisibilityChanged() => VisibilityChanged?.Invoke();

    // label → hours override (e.g. "unified-5h" → 48)
    public Dictionary<string, double> ChartWindowHours { get; private set; } = new();

    public string TogglApiKey { get; set; } = "";
    public double TogglMonthlyTargetCzk { get; set; }
    // project_id → CZK/hour
    public Dictionary<long, double> TogglProjectRates { get; private set; } = new();

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
            AlwaysOnTop = (int)(key.GetValue("AlwaysOnTop", 1) ?? 1) != 0;
            ShowClaude = (int)(key.GetValue("ShowClaude", 1) ?? 1) != 0;
            ShowCodex = (int)(key.GetValue("ShowCodex", 1) ?? 1) != 0;
            ShowToggl = (int)(key.GetValue("ShowToggl", 1) ?? 1) != 0;
            TogglApiKey = key.GetValue("TogglApiKey") as string ?? "";
            var targetRaw = key.GetValue("TogglMonthlyTargetCzk") as string;
            if (!string.IsNullOrEmpty(targetRaw) &&
                double.TryParse(targetRaw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var target))
                TogglMonthlyTargetCzk = target;
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
            key.SetValue("AlwaysOnTop", AlwaysOnTop ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowClaude", ShowClaude ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowCodex", ShowCodex ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowToggl", ShowToggl ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("TogglApiKey", TogglApiKey, RegistryValueKind.String);
            key.SetValue("TogglMonthlyTargetCzk",
                TogglMonthlyTargetCzk.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RegistryValueKind.String);
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
