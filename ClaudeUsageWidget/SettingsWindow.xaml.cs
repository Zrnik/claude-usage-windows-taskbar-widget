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
    private readonly Dictionary<string, CheckBox> _limitShowChecks = new();
    private readonly Dictionary<long, Control> _togglRateBoxes = new();

    public SettingsWindow()
    {
        InitializeComponent();
        Icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/claude-logo.png"));
        VersionText.Text = $"v{Updater.CurrentVersion}";

        var settings = SettingsStore.Instance;
        ShowClaudeCheck.IsChecked = settings.ShowClaude;
        ShowCodexCheck.IsChecked = settings.ShowCodex;
        ShowTogglCheck.IsChecked = settings.ShowToggl;
        ShowJiraCheck.IsChecked = settings.ShowJira;
        ClaudeWidthBox.Text = settings.ClaudeWidth.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        CodexWidthBox.Text = settings.CodexWidth.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        TogglWidthBox.Text = settings.TogglWidth.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        JiraWidthBox.Text = settings.JiraWidth.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        ClaudeWidthBox.TextChanged += (_, _) => SaveVisibility();
        CodexWidthBox.TextChanged += (_, _) => SaveVisibility();
        TogglWidthBox.TextChanged += (_, _) => SaveVisibility();
        JiraWidthBox.TextChanged += (_, _) => SaveVisibility();
        NotificationsCheck.IsChecked = settings.NotificationsEnabled;
        NotifyResetCheck.IsChecked = settings.NotifyOnReset;
        StartupCheck.IsChecked = IsStartupEnabled();
        DesktopShortcutCheck.IsChecked = MainWindow.DesktopShortcutExists();

#if DEBUG
        StartupCheck.IsEnabled = false;
        DesktopShortcutCheck.IsEnabled = false;
#endif

        BuildChartWindowsUI(settings);
        InitTogglUI(settings);
        InitJiraUI(settings);
        ApplyIncognitoVisibility();
        SettingsStore.KnownLabelsChanged += OnKnownLabelsChanged;
        SettingsStore.IncognitoChanged += OnIncognitoChangedFromOutside;
        Closed += (_, _) =>
        {
            SettingsStore.KnownLabelsChanged -= OnKnownLabelsChanged;
            SettingsStore.IncognitoChanged -= OnIncognitoChangedFromOutside;
        };

        TogglRefreshButton.Click += (_, _) => SettingsStore.RaiseTogglRefreshRequested();
        JiraRefreshButton.Click += (_, _) => SettingsStore.RaiseJiraRefreshRequested();

        ShowClaudeCheck.Checked += (_, _) => SaveVisibility();
        ShowClaudeCheck.Unchecked += (_, _) => SaveVisibility();
        ShowCodexCheck.Checked += (_, _) => SaveVisibility();
        ShowCodexCheck.Unchecked += (_, _) => SaveVisibility();
        ShowTogglCheck.Checked += (_, _) => SaveVisibility();
        ShowTogglCheck.Unchecked += (_, _) => SaveVisibility();
        ShowJiraCheck.Checked += (_, _) => SaveVisibility();
        ShowJiraCheck.Unchecked += (_, _) => SaveVisibility();
        NotificationsCheck.Checked += (_, _) => SaveSettings();
        NotificationsCheck.Unchecked += (_, _) => SaveSettings();
        NotifyResetCheck.Checked += (_, _) => SaveSettings();
        NotifyResetCheck.Unchecked += (_, _) => SaveSettings();
        StartupCheck.Checked += (_, _) => SetStartup(true);
        StartupCheck.Unchecked += (_, _) => SetStartup(false);
        DesktopShortcutCheck.Checked += (_, _) => MainWindow.CreateDesktopShortcut();
        DesktopShortcutCheck.Unchecked += (_, _) => MainWindow.RemoveDesktopShortcut();

        CloseButton.Click += (_, _) => SafeClose();
    }

    private void SaveVisibility()
    {
        var settings = SettingsStore.Instance;
        settings.ShowClaude = ShowClaudeCheck.IsChecked == true;
        settings.ShowCodex = ShowCodexCheck.IsChecked == true;
        settings.ShowToggl = ShowTogglCheck.IsChecked == true;
        settings.ShowJira = ShowJiraCheck.IsChecked == true;
        settings.ClaudeWidth = ParseWidthOrDefault(ClaudeWidthBox.Text, settings.ClaudeWidth);
        settings.CodexWidth = ParseWidthOrDefault(CodexWidthBox.Text, settings.CodexWidth);
        settings.TogglWidth = ParseWidthOrDefault(TogglWidthBox.Text, settings.TogglWidth);
        settings.JiraWidth = ParseWidthOrDefault(JiraWidthBox.Text, settings.JiraWidth);
        settings.Save();
        SettingsStore.RaiseVisibilityChanged();
    }

    private static double ParseWidthOrDefault(string raw, double fallback)
    {
        if (double.TryParse(raw.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var w) && w >= 50 && w <= 600)
            return w;
        return fallback;
    }

    private readonly Dictionary<string, CheckBox> _jiraDevCheckboxes = new();

    private bool _suppressJiraProjectChange;

    private void InitJiraUI(SettingsStore settings)
    {
        JiraUrlBox.Text = settings.JiraUrl;
        JiraEmailBox.Text = settings.JiraEmail;
        JiraTokenBox.Password = settings.JiraApiToken;

        JiraUrlBox.LostFocus += (_, _) => SaveAndValidateJiraSafe();
        JiraEmailBox.LostFocus += (_, _) => SaveAndValidateJiraSafe();
        JiraTokenBox.LostFocus += (_, _) => SaveAndValidateJiraSafe();
        JiraProjectCombo.SelectionChanged += (_, _) =>
        {
            if (_suppressJiraProjectChange) return;
            SaveAndValidateJiraSafe();
        };

        if (HasJiraUrlEmailToken(settings))
            SaveAndValidateJiraSafe();
    }

    private static bool HasJiraUrlEmailToken(SettingsStore s) =>
        !string.IsNullOrWhiteSpace(s.JiraUrl) &&
        !string.IsNullOrWhiteSpace(s.JiraEmail) &&
        !string.IsNullOrWhiteSpace(s.JiraApiToken);

    private void SaveAndValidateJiraSafe()
    {
        try { SaveJira(); }
        catch (Exception ex) { JiraStatusText.Text = $"✗ Save failed: {ex.Message}"; return; }
        _ = SafeRunAsync(() => ValidateJiraAsync(), JiraStatusText);
    }

    private static async Task SafeRunAsync(Func<Task> action, TextBlock? statusBlock)
    {
        try { await action(); }
        catch (Exception ex)
        {
            try
            {
                if (statusBlock != null)
                {
                    statusBlock.Text = $"✗ {ex.Message}";
                    statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                }
            }
            catch { /* UI may be disposed */ }
        }
    }

    private void SaveJira()
    {
        var settings = SettingsStore.Instance;
        settings.JiraUrl = NormalizeJiraUrl(JiraUrlBox.Text);
        settings.JiraEmail = JiraEmailBox.Text.Trim();
        settings.JiraApiToken = JiraTokenBox.Password.Trim();
        settings.JiraProjectKey = (JiraProjectCombo.SelectedItem as JiraProject)?.Key ?? settings.JiraProjectKey;
        settings.Save();
        SettingsStore.RaiseVisibilityChanged();
    }

    private static string NormalizeJiraUrl(string raw)
    {
        var s = raw.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(s)) return "";
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            s = "https://" + s;
        return s;
    }

    private async Task ValidateJiraAsync()
    {
        var settings = SettingsStore.Instance;
        if (!HasJiraUrlEmailToken(settings))
        {
            JiraStatusText.Text = "";
            JiraProjectCombo.ItemsSource = null;
            JiraDevsPanel.Children.Clear();
            JiraDevsLabel.Visibility = Visibility.Collapsed;
            _jiraDevCheckboxes.Clear();
            return;
        }

        JiraStatusText.Text = "Validating…";
        JiraStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        var (ok, error) = await JiraApiClient.ValidateCredsAsync(
            settings.JiraUrl, settings.JiraEmail, settings.JiraApiToken);
        if (!ok)
        {
            JiraStatusText.Text = $"✗ {error}";
            JiraStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
            JiraProjectCombo.ItemsSource = null;
            JiraDevsPanel.Children.Clear();
            JiraDevsLabel.Visibility = Visibility.Collapsed;
            return;
        }

        JiraStatusText.Text = "✓ Connected — loading projects…";
        JiraStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

        var client = new JiraApiClient();
        var me = await client.FetchMyselfAsync(settings.JiraUrl, settings.JiraEmail, settings.JiraApiToken);
        var projects = await client.FetchProjectsAsync();
        PopulateProjectCombo(projects, settings.JiraProjectKey);

        if (string.IsNullOrWhiteSpace(settings.JiraProjectKey))
        {
            JiraStatusText.Text = $"✓ Connected — pick a project ({projects.Count} available)";
            JiraDevsPanel.Children.Clear();
            JiraDevsLabel.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var users = await client.FetchAssignableUsersAsync(settings.JiraProjectKey);
            // Exclude current user from "compare with developers" — comparing yourself to yourself is pointless
            var others = me != null
                ? users.Where(u => u.AccountId != me.AccountId).ToList()
                : users.ToList();
            BuildJiraDevsUI(others);
            JiraStatusText.Text = $"✓ Connected — {others.Count} other user(s) in {settings.JiraProjectKey}";
        }
        catch (Exception ex)
        {
            JiraStatusText.Text = $"✗ Failed to load users: {ex.Message}";
            JiraStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
        }
    }

    private void PopulateProjectCombo(IReadOnlyList<JiraProject> projects, string currentKey)
    {
        _suppressJiraProjectChange = true;
        try
        {
            // ItemTemplate (in XAML) handles display — don't also set DisplayMemberPath (XAML conflict)
            JiraProjectCombo.ItemsSource = projects;
            JiraProjectCombo.SelectedItem = projects.FirstOrDefault(p =>
                string.Equals(p.Key, currentKey, StringComparison.OrdinalIgnoreCase));
        }
        finally { _suppressJiraProjectChange = false; }
    }

    private void BuildJiraDevsUI(IReadOnlyList<JiraUser> users)
    {
        JiraDevsPanel.Children.Clear();
        _jiraDevCheckboxes.Clear();

        if (users.Count == 0)
        {
            JiraDevsLabel.Visibility = Visibility.Collapsed;
            return;
        }

        JiraDevsLabel.Visibility = Visibility.Visible;
        var settings = SettingsStore.Instance;

        foreach (var u in users)
        {
            var cb = new CheckBox
            {
                Content = u.DisplayName + (u.EmailAddress != null ? $"  ({u.EmailAddress})" : ""),
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2),
                IsChecked = settings.JiraDeveloperAccountIds.Contains(u.AccountId),
                ToolTip = u.AccountId
            };
            cb.Checked += (_, _) => SaveJiraDevs();
            cb.Unchecked += (_, _) => SaveJiraDevs();
            JiraDevsPanel.Children.Add(cb);
            _jiraDevCheckboxes[u.AccountId] = cb;
        }
    }

    private void SaveJiraDevs()
    {
        var settings = SettingsStore.Instance;
        settings.JiraDeveloperAccountIds.Clear();
        foreach (var (accountId, cb) in _jiraDevCheckboxes)
            if (cb.IsChecked == true)
                settings.JiraDeveloperAccountIds.Add(accountId);
        settings.Save();
        SettingsStore.RaiseVisibilityChanged();
    }

    private void InitTogglUI(SettingsStore settings)
    {
        TogglApiKeyBox.Password = settings.TogglApiKey;
        var targetText = settings.TogglMonthlyTargetCzk > 0
            ? settings.TogglMonthlyTargetCzk.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : "";
        TogglTargetBox.Text = targetText;
        TogglTargetPasswordBox.Password = targetText;
        WorkdayStartBox.Text = settings.WorkdayStartHour.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        WorkdayEndBox.Text = settings.WorkdayEndHour.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        TogglApiKeyBox.LostFocus += (_, _) =>
        {
            _ = SafeRunAsync(() => OnTogglKeyChangedAsync(), TogglStatusText);
        };
        TogglTargetBox.LostFocus += (_, _) => SaveSettings();
        TogglTargetPasswordBox.LostFocus += (_, _) => SaveSettings();
        WorkdayStartBox.LostFocus += (_, _) => SaveSettings();
        WorkdayEndBox.LostFocus += (_, _) => SaveSettings();

        if (!string.IsNullOrWhiteSpace(settings.TogglApiKey))
            _ = ValidateAndLoadProjectsAsync(settings.TogglApiKey);
    }

    private async Task OnTogglKeyChangedAsync()
    {
        var newKey = TogglApiKeyBox.Password.Trim();
        var settings = SettingsStore.Instance;
        if (newKey == settings.TogglApiKey) return;
        settings.TogglApiKey = newKey;
        settings.Save();
        SettingsStore.RaiseVisibilityChanged();

        if (string.IsNullOrEmpty(newKey))
        {
            TogglStatusText.Text = "";
            TogglProjectsPanel.Children.Clear();
            TogglProjectsLabel.Visibility = Visibility.Collapsed;
            _togglRateBoxes.Clear();
            return;
        }

        await ValidateAndLoadProjectsAsync(newKey);
    }

    private async Task ValidateAndLoadProjectsAsync(string apiKey)
    {
        TogglStatusText.Text = "Validating…";
        TogglStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        var (ok, error) = await TogglApiClient.ValidateKeyAsync(apiKey);
        if (!ok)
        {
            TogglStatusText.Text = $"✗ {error}";
            TogglStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
            TogglProjectsPanel.Children.Clear();
            TogglProjectsLabel.Visibility = Visibility.Collapsed;
            return;
        }

        TogglStatusText.Text = "✓ Connected — loading projects…";
        TogglStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

        try
        {
            var client = new TogglApiClient();
            var projects = await client.FetchProjectsAsync(apiKey);
            BuildTogglProjectsUI(projects);
            TogglStatusText.Text = $"✓ Connected — {projects.Count} project(s)";
        }
        catch (Exception ex)
        {
            TogglStatusText.Text = $"✗ Failed to load projects: {ex.Message}";
            TogglStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
        }
    }

    private void BuildTogglProjectsUI(IReadOnlyList<TogglProject> projects)
    {
        TogglProjectsPanel.Children.Clear();
        _togglRateBoxes.Clear();

        if (projects.Count == 0)
        {
            TogglProjectsLabel.Visibility = Visibility.Collapsed;
            return;
        }

        TogglProjectsLabel.Visibility = Visibility.Visible;
        var settings = SettingsStore.Instance;

        foreach (var p in projects.Where(p => p.Active))
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var labelText = p.ClientName != null ? $"{p.ClientName} / {p.Name}" : p.Name;
            var labelBlock = new TextBlock
            {
                Text = labelText,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = labelText
            };
            Grid.SetColumn(labelBlock, 0);

            var currentRate = settings.TogglProjectRates.TryGetValue(p.Id, out var r) ? r : 0;
            var rateText = currentRate > 0
                ? currentRate.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                : "";
            Control box;
            if (settings.IncognitoMode)
            {
                var pb = new PasswordBox
                {
                    Password = rateText,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                    FontSize = 10,
                    Padding = new Thickness(4, 2, 4, 2),
                    HorizontalContentAlignment = HorizontalAlignment.Right
                };
                pb.LostFocus += (_, _) => SaveSettings();
                box = pb;
            }
            else
            {
                var tb = new TextBox
                {
                    Text = rateText,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                    FontSize = 10,
                    Padding = new Thickness(4, 2, 4, 2),
                    HorizontalContentAlignment = HorizontalAlignment.Right
                };
                tb.LostFocus += (_, _) => SaveSettings();
                box = tb;
            }
            Grid.SetColumn(box, 1);

            grid.Children.Add(labelBlock);
            grid.Children.Add(box);
            TogglProjectsPanel.Children.Add(grid);

            _togglRateBoxes[p.Id] = box;
        }
    }

    private void RebuildChartWindowsUI()
    {
        ChartWindowsPanel.Children.Clear();
        _chartWindowBoxes.Clear();
        _limitShowChecks.Clear();
        BuildChartWindowsUI(SettingsStore.Instance);
    }

    private void OnKnownLabelsChanged() => Dispatcher.Invoke(RebuildChartWindowsUI);

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

            // 3 sloupce: [checkbox show] [label] [hours]
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            var showCheck = new CheckBox
            {
                IsChecked = !settings.IsLimitHidden(label),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Show this rate limit bar"
            };
            showCheck.Checked += (_, _) => SaveSettings();
            showCheck.Unchecked += (_, _) => SaveSettings();
            Grid.SetColumn(showCheck, 0);

            var labelBlock = new TextBlock
            {
                Text = display,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelBlock, 1);

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
            Grid.SetColumn(box, 2);

            grid.Children.Add(showCheck);
            grid.Children.Add(labelBlock);
            grid.Children.Add(box);
            ChartWindowsPanel.Children.Add(grid);

            _chartWindowBoxes[label] = box;
            _limitShowChecks[label] = showCheck;
        }
    }

    private void ApplyIncognitoVisibility()
    {
        bool incognito = SettingsStore.Instance.IncognitoMode;
        if (incognito)
        {
            TogglTargetPasswordBox.Password = TogglTargetBox.Text;
            TogglTargetBox.Visibility = Visibility.Collapsed;
            TogglTargetPasswordBox.Visibility = Visibility.Visible;
        }
        else
        {
            TogglTargetBox.Text = TogglTargetPasswordBox.Password;
            TogglTargetPasswordBox.Visibility = Visibility.Collapsed;
            TogglTargetBox.Visibility = Visibility.Visible;
        }

        // Rebuild rate boxes so they swap between TextBox and PasswordBox
        var settings = SettingsStore.Instance;
        var apiKey = settings.TogglApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
            _ = SafeRunAsync(() => ValidateAndLoadProjectsAsync(apiKey), TogglStatusText);
    }

    private void OnIncognitoChangedFromOutside() => Dispatcher.Invoke(ApplyIncognitoVisibility);

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

        foreach (var (label, check) in _limitShowChecks)
        {
            if (check.IsChecked == true) settings.HiddenLimits.Remove(label);
            else settings.HiddenLimits.Add(label);
        }
        // Změna HiddenLimits → MainWindow musí překreslit bary
        SettingsStore.RaiseVisibilityChanged();

        var targetRaw = (SettingsStore.Instance.IncognitoMode
            ? TogglTargetPasswordBox.Password
            : TogglTargetBox.Text).Trim();
        if (string.IsNullOrEmpty(targetRaw))
        {
            settings.TogglMonthlyTargetCzk = 0;
        }
        else if (double.TryParse(targetRaw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var target) && target >= 0)
        {
            settings.TogglMonthlyTargetCzk = target;
        }
        // Mirror raw value to the inactive control so toggling incognito keeps state in sync
        if (SettingsStore.Instance.IncognitoMode)
            TogglTargetBox.Text = targetRaw;
        else
            TogglTargetPasswordBox.Password = targetRaw;

        if (double.TryParse(WorkdayStartBox.Text.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var ws) && ws is >= 0 and <= 24)
            settings.WorkdayStartHour = ws;
        if (double.TryParse(WorkdayEndBox.Text.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var we) && we is >= 0 and <= 24)
            settings.WorkdayEndHour = we;

        foreach (var (projectId, box) in _togglRateBoxes)
        {
            var raw = (box switch
            {
                TextBox tb => tb.Text,
                PasswordBox pb => pb.Password,
                _ => ""
            }).Trim();
            if (string.IsNullOrEmpty(raw))
            {
                settings.TogglProjectRates.Remove(projectId);
            }
            else if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rate) && rate >= 0)
            {
                if (rate > 0) settings.TogglProjectRates[projectId] = rate;
                else settings.TogglProjectRates.Remove(projectId);
            }
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
