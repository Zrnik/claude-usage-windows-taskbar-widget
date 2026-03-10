using System.Windows;

namespace ClaudeUsageWidgetProvider;

public partial class SettingsWindow : Window
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunRegistryValue = "ClaudeUsageWidget";

    public SettingsWindow()
    {
        InitializeComponent();

        var settings = SettingsStore.Instance;
        NotificationsCheck.IsChecked = settings.NotificationsEnabled;
        NotifyResetCheck.IsChecked = settings.NotifyOnReset;
        StartupCheck.IsChecked = IsStartupEnabled();

#if DEBUG
        StartupCheck.IsEnabled = false;
#endif

        NotificationsCheck.Checked += (_, _) => SaveSettings();
        NotificationsCheck.Unchecked += (_, _) => SaveSettings();
        NotifyResetCheck.Checked += (_, _) => SaveSettings();
        NotifyResetCheck.Unchecked += (_, _) => SaveSettings();
        StartupCheck.Checked += (_, _) => SetStartup(true);
        StartupCheck.Unchecked += (_, _) => SetStartup(false);

        CloseButton.Click += (_, _) => Close();
        Deactivated += (_, _) => Close();
    }

    private void SaveSettings()
    {
        var settings = SettingsStore.Instance;
        settings.NotificationsEnabled = NotificationsCheck.IsChecked == true;
        settings.NotifyOnReset = NotifyResetCheck.IsChecked == true;
        settings.Save();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryKey);
        return key?.GetValue(RunRegistryValue) != null;
    }

    private static void SetStartup(bool enable)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
        if (key == null) return;
        if (enable)
            key.SetValue(RunRegistryValue, $"\"{System.Environment.ProcessPath}\"");
        else
            key.DeleteValue(RunRegistryValue, throwOnMissingValue: false);
    }
}
