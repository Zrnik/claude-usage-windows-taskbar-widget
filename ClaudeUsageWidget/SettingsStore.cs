using Microsoft.Win32;

namespace ClaudeUsageWidgetProvider;

internal sealed class SettingsStore
{
    public static readonly SettingsStore Instance = new();

    private const string RegistryPath = @"Software\ClaudeUsageWidget";
    private const string ChartWindowsSubKey = @"Software\ClaudeUsageWidget\ChartWindows";

    public bool NotificationsEnabled { get; set; }
    public bool NotifyOnReset { get; set; }
    public bool AlwaysOnTop { get; set; } = true;

    // label → hours override (e.g. "unified-5h" → 48)
    public Dictionary<string, double> ChartWindowHours { get; private set; } = new();

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
        }
        catch { }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ChartWindowsSubKey);
            if (key == null) return;
            foreach (var name in key.GetValueNames())
            {
                var raw = key.GetValue(name);
                double hours = 0;
                if (raw is int intVal) hours = intVal; // legacy: days stored as DWord
                else if (raw is string str) double.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, out hours);
                if (hours > 0) ChartWindowHours[name] = hours;
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
        }
        catch { }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ChartWindowsSubKey);
            foreach (var (label, hours) in ChartWindowHours)
                key.SetValue(label, hours.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
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
