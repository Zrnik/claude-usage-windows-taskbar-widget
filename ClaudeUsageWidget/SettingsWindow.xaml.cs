using System.Windows;
using System.Windows.Input;

namespace ClaudeUsageWidgetProvider;

public partial class SettingsWindow : Window
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunRegistryValue = "ClaudeUsageWidget";
    private bool _closing;

    public SettingsWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{Updater.CurrentVersion}";

        var settings = SettingsStore.Instance;
        NotificationsCheck.IsChecked = settings.NotificationsEnabled;
        NotifyResetCheck.IsChecked = settings.NotifyOnReset;
        SpendLimitBox.Text = settings.SpendLimit.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        StartupCheck.IsChecked = IsStartupEnabled();

#if DEBUG
        StartupCheck.IsEnabled = false;
#endif

        NotificationsCheck.Checked += (_, _) => SaveSettings();
        NotificationsCheck.Unchecked += (_, _) => SaveSettings();
        NotifyResetCheck.Checked += (_, _) => SaveSettings();
        NotifyResetCheck.Unchecked += (_, _) => SaveSettings();
        SpendLimitBox.LostFocus += (_, _) => SaveSettings();
        StartupCheck.Checked += (_, _) => SetStartup(true);
        StartupCheck.Unchecked += (_, _) => SetStartup(false);

        CloseButton.Click += (_, _) => SafeClose();
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void SaveSettings()
    {
        var settings = SettingsStore.Instance;
        settings.NotificationsEnabled = NotificationsCheck.IsChecked == true;
        settings.NotifyOnReset = NotifyResetCheck.IsChecked == true;
        if (double.TryParse(SpendLimitBox.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var spend) && spend >= 0)
            settings.SpendLimit = spend;
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
