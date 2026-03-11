using Microsoft.Win32;

namespace ClaudeUsageWidgetProvider;

internal sealed class SettingsStore
{
    public static readonly SettingsStore Instance = new();

    private const string RegistryPath = @"Software\ClaudeUsageWidget";

    public bool NotificationsEnabled { get; set; }
    public bool NotifyOnReset { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public double SpendLimit { get; set; } = 20.0;

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
            var spendStr = key.GetValue("SpendLimit") as string;
            if (spendStr != null && double.TryParse(spendStr, System.Globalization.CultureInfo.InvariantCulture, out var spend))
                SpendLimit = spend;
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
            key.SetValue("SpendLimit", SpendLimit.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
        }
        catch { }
    }
}
