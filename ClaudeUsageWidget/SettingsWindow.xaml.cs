using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClaudeUsageWidgetProvider;

public partial class SettingsWindow : Window
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunRegistryValue = "ClaudeUsageWidget";
    private bool _closing;
    private readonly Dictionary<string, TextBox> _chartWindowBoxes = new();

    public SettingsWindow()
    {
        InitializeComponent();
        Icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/claude-logo.png"));
        VersionText.Text = $"v{Updater.CurrentVersion}";

        var settings = SettingsStore.Instance;
        NotificationsCheck.IsChecked = settings.NotificationsEnabled;
        NotifyResetCheck.IsChecked = settings.NotifyOnReset;
        StartupCheck.IsChecked = IsStartupEnabled();

#if DEBUG
        StartupCheck.IsEnabled = false;
#endif

        BuildChartWindowsUI(settings);

        NotificationsCheck.Checked += (_, _) => SaveSettings();
        NotificationsCheck.Unchecked += (_, _) => SaveSettings();
        NotifyResetCheck.Checked += (_, _) => SaveSettings();
        NotifyResetCheck.Unchecked += (_, _) => SaveSettings();
        StartupCheck.Checked += (_, _) => SetStartup(true);
        StartupCheck.Unchecked += (_, _) => SetStartup(false);

        CloseButton.Click += (_, _) => SafeClose();
    }

    private void BuildChartWindowsUI(SettingsStore settings)
    {
        var labels = settings.GetKnownLabels();
        if (labels.Count == 0) return;

        ChartWindowsPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            Margin = new Thickness(0, 4, 0, 8)
        });

        ChartWindowsPanel.Children.Add(new TextBlock
        {
            Text = "Chart time window (hours)",
            Foreground = Brushes.White,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 6)
        });

        foreach (var (label, display) in labels)
        {
            var currentHours = settings.ChartWindowHours.TryGetValue(label, out var h)
                ? h : HistoryChart.GetDefaultHours(label);

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            var labelBlock = new TextBlock
            {
                Text = display,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelBlock, 0);

            var box = new TextBox
            {
                Text = currentHours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                FontSize = 10,
                Padding = new Thickness(4, 2, 4, 2),
                HorizontalContentAlignment = HorizontalAlignment.Right
            };
            box.LostFocus += (_, _) => SaveSettings();
            Grid.SetColumn(box, 1);

            grid.Children.Add(labelBlock);
            grid.Children.Add(box);
            ChartWindowsPanel.Children.Add(grid);

            _chartWindowBoxes[label] = box;
        }
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

        foreach (var (label, box) in _chartWindowBoxes)
        {
            if (double.TryParse(box.Text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var hours) && hours > 0)
                settings.ChartWindowHours[label] = hours;
        }

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
