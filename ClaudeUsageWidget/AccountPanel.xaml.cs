using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudeUsageWidgetProvider;

public partial class AccountPanel : UserControl
{
    private static readonly string[] SpinnerFrames = ["|", "/", "—", "\\"];
    private int _spinnerFrame;
    private bool _isLoading;

    private readonly List<(ProgressBar Bar, TextBlock PctText, TextBlock TimeText, Grid Container)> _bars = [];

    internal AccountPanel(ServiceType service)
    {
        InitializeComponent();
        ServiceIcon.Source = new BitmapImage(new Uri(service switch
        {
            ServiceType.Claude => "pack://application:,,,/Assets/claude-logo.png",
            ServiceType.Codex => "pack://application:,,,/Assets/codex-logo.png",
            ServiceType.Toggl => "pack://application:,,,/Assets/toggl-logo.png",
            _ => "pack://application:,,,/Assets/claude-logo.png"
        }));
    }

    public void UpdateBars(UsageData data)
    {
        _isLoading = false;
        EnsureBarCount(data.Limits.Count);

        for (int i = 0; i < data.Limits.Count; i++)
        {
            var limit = data.Limits[i];
            var (bar, pctText, timeText, container) = _bars[i];

            bar.Value = limit.Utilization;
            SetBarColor(GetBarIndicator(bar), limit.Utilization);

            bool showText = data.Limits.Count <= 4;
            pctText.Text = showText ? $"{limit.Utilization:0}%" : "";
            timeText.Text = showText ? TimeFormatter.FormatResetTime(limit.ResetsAt) : "";

            container.ToolTip = null;
            container.Tag = null;
            container.ContextMenu = null;
        }
    }

    private TextBlock? _togglLine1;
    private TextBlock? _togglLine2;
    private ProgressBar? _togglBar;
    private TextBlock? _togglBarOverlay;

    public void UpdateTogglBars(TogglUsageData data)
    {
        _isLoading = false;
        _bars.Clear();
        BarsPanel.RowDefinitions.Clear();
        BarsPanel.Children.Clear();

        // Row layout: bar (compact) | text1 | text2
        BarsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        BarsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Pixel) });
        BarsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        BarsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Build bar
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0 };
        bar.Template = CreateBarTemplate();
        double pct = data.TargetCzk > 0
            ? Math.Min(100.0, data.EarnedCzk / data.TargetCzk * 100.0)
            : 0.0;
        bar.Value = pct;

        var barOverlay = new TextBlock
        {
            Text = data.TargetCzk > 0
                ? $"{pct:0}%  {FormatCzk(data.EarnedCzk)} / {FormatCzk(data.TargetCzk)}"
                : $"{FormatCzk(data.EarnedCzk)}  (bez cíle)",
            Foreground = Brushes.White,
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var barContainer = new Grid();
        barContainer.Children.Add(bar);
        barContainer.Children.Add(barOverlay);
        Grid.SetRow(barContainer, 0);
        BarsPanel.Children.Add(barContainer);
        _togglBar = bar;
        _togglBarOverlay = barOverlay;
        SetTogglBarColor(GetBarIndicator(bar), data.EarnedCzk, data.TargetCzk);

        // Compute pace info (same formula as PopupWindow)
        var now = DateTimeOffset.Now;
        int wdTotal = CountWorkingDays(data.MonthStart, data.MonthResetsAt.AddDays(-1));
        int wdElapsed = CountWorkingDays(data.MonthStart, now);
        int wdRemaining = Math.Max(0, wdTotal - wdElapsed);
        double remaining = Math.Max(0, data.TargetCzk - data.EarnedCzk);
        double impliedRate = (wdTotal > 0 && data.TargetCzk > 0) ? data.TargetCzk / (wdTotal * 8.0) : 0;
        double reqHoursPerDay = (impliedRate > 0 && wdRemaining > 0) ? remaining / (impliedRate * wdRemaining) : 0;

        string line1;
        Color line1Color;
        if (data.TargetCzk <= 0)
        {
            line1 = "Bez nastaveného cíle";
            line1Color = Color.FromRgb(0x88, 0x88, 0x88);
        }
        else if (remaining <= 0)
        {
            line1 = "✓ Cíl splněn";
            line1Color = Color.FromRgb(0x21, 0x96, 0xF3);
        }
        else if (wdRemaining <= 0)
        {
            line1 = "Měsíc skončil";
            line1Color = Color.FromRgb(0xF4, 0x43, 0x36);
        }
        else
        {
            line1 = $"Ideálně {reqHoursPerDay:0.#} h/den";
            line1Color = reqHoursPerDay <= 8
                ? Color.FromRgb(0x4C, 0xAF, 0x50)
                : reqHoursPerDay <= 10
                    ? Color.FromRgb(0xFF, 0x98, 0x00)
                    : Color.FromRgb(0xF4, 0x43, 0x36);
        }

        var line1Block = new TextBlock
        {
            Text = line1,
            Foreground = new SolidColorBrush(line1Color),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(line1Block, 2);
        BarsPanel.Children.Add(line1Block);
        _togglLine1 = line1Block;

        string line2;
        if (wdRemaining > 0 && remaining > 0 && data.TargetCzk > 0)
        {
            double perDay = remaining / wdRemaining;
            line2 = $"Zbývá {wdRemaining}d · {FormatCzk(perDay)}/den";
        }
        else
        {
            line2 = $"{data.HoursWorked:0.#}h odpracováno";
        }

        var line2Block = new TextBlock
        {
            Text = line2,
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(line2Block, 3);
        BarsPanel.Children.Add(line2Block);
        _togglLine2 = line2Block;
    }

    private static int CountWorkingDays(DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from) return 0;
        int count = 0;
        var d = from.Date;
        var end = to.Date;
        while (d <= end)
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                count++;
            d = d.AddDays(1);
        }
        return count;
    }

    private static string FormatCzk(double czk)
    {
        if (czk >= 1_000_000) return $"{czk / 1_000_000.0:0.#}M Kč";
        if (czk >= 10_000) return $"{czk / 1000.0:0}k Kč";
        if (czk >= 1000) return $"{czk / 1000.0:0.#}k Kč";
        return $"{czk:0} Kč";
    }

    private static void SetTogglBarColor(Border? indicator, double earned, double target)
    {
        if (indicator == null) return;
        // Toggl: green while building toward target, blue when target reached/exceeded
        string color;
        if (target <= 0) color = "#888888";
        else if (earned >= target) color = "#2196F3"; // blue — goal reached
        else color = "#4CAF50";                        // green — in progress
        indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    public void ShowLoadingState()
    {
        _isLoading = true;
        EnsureBarCount(2);
        foreach (var (bar, pctText, timeText, container) in _bars)
        {
            bar.Value = 0;
            container.ToolTip = null;
            container.ContextMenu = null;
            pctText.Text = SpinnerFrames[_spinnerFrame];
            timeText.Text = "";
        }
    }

    public void AdvanceSpinner()
    {
        if (!_isLoading) return;
        _spinnerFrame = (_spinnerFrame + 1) % SpinnerFrames.Length;
        foreach (var (_, pctText, _, _) in _bars)
            pctText.Text = SpinnerFrames[_spinnerFrame];
    }

    public void ClearSpinner()
    {
        foreach (var (_, pctText, timeText, _) in _bars)
        {
            pctText.Text = "";
            timeText.Text = "";
        }
    }

    public void ShowErrorState(string? errorMessage = null)
    {
        _isLoading = false;
        EnsureBarCount(Math.Max(_bars.Count, 2));
        var maroon = new SolidColorBrush(Colors.Maroon);
        foreach (var (bar, pctText, timeText, container) in _bars)
        {
            bar.Value = 100;
            var ind = GetBarIndicator(bar);
            if (ind != null) ind.Background = maroon;
            pctText.Foreground = Brushes.White;
            pctText.Text = "Error";
            timeText.Text = "";

            if (!string.IsNullOrEmpty(errorMessage))
            {
                container.ToolTip = errorMessage;
                container.Tag = errorMessage;
                container.ContextMenu = CreateCopyErrorMenu(errorMessage);
            }
            else
            {
                container.ToolTip = null;
                container.Tag = null;
                container.ContextMenu = null;
            }
        }
    }

    private static ContextMenu CreateCopyErrorMenu(string error)
    {
        var menu = new ContextMenu();
        var item = new MenuItem { Header = "Copy error" };
        item.Click += (_, _) => Clipboard.SetText(error);
        menu.Items.Add(item);
        return menu;
    }

    public void RefreshText(UsageData? lastUsage)
    {
        if (lastUsage == null) return;
        for (int i = 0; i < lastUsage.Limits.Count && i < _bars.Count; i++)
        {
            var limit = lastUsage.Limits[i];
            bool showText = lastUsage.Limits.Count <= 4;
            _bars[i].PctText.Text = showText ? $"{limit.Utilization:0}%" : "";
            _bars[i].TimeText.Text = showText ? TimeFormatter.FormatResetTime(limit.ResetsAt) : "";
            _bars[i].Container.ToolTip = null;
        }
    }

    private void EnsureBarCount(int count)
    {
        // Remove excess bars
        while (_bars.Count > count)
        {
            BarsPanel.Children.RemoveAt(BarsPanel.Children.Count - 1);
            _bars.RemoveAt(_bars.Count - 1);
        }

        // Add missing bars
        while (_bars.Count < count)
        {
            var entry = CreateBarEntry();
            _bars.Add(entry);
            BarsPanel.Children.Add(entry.Container);
        }

        // Rebuild row definitions: bar* / spacing / bar* / spacing / bar*
        double spacing = count <= 2 ? 5 : count <= 4 ? 3 : 2;
        BarsPanel.RowDefinitions.Clear();
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                BarsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(spacing, GridUnitType.Pixel) });
            BarsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            int row = i == 0 ? 0 : i * 2; // bar rows: 0, 2, 4, ...
            Grid.SetRow(_bars[i].Container, row);
        }
    }

    private static (ProgressBar Bar, TextBlock PctText, TextBlock TimeText, Grid Container) CreateBarEntry()
    {
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0 };
        bar.Template = CreateBarTemplate();

        // Overlay grid: [0..35%] pct right-aligned | [55%..100%] time left-aligned
        var overlay = new Grid();
        overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35, GridUnitType.Star) });
        overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });
        overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45, GridUnitType.Star) });

        var pctText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 9
        };
        Grid.SetColumn(pctText, 0);

        var timeText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 9
        };
        Grid.SetColumn(timeText, 2);

        overlay.Children.Add(pctText);
        overlay.Children.Add(timeText);

        var container = new Grid();
        container.Children.Add(bar);
        container.Children.Add(overlay);

        return (bar, pctText, timeText, container);
    }

    private static ControlTemplate CreateBarTemplate()
    {
        var template = new ControlTemplate(typeof(ProgressBar));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        var bgFactory = new FrameworkElementFactory(typeof(Border));
        bgFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
        bgFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

        var trackFactory = new FrameworkElementFactory(typeof(Border));
        trackFactory.Name = "PART_Track";

        var indicatorFactory = new FrameworkElementFactory(typeof(Border));
        indicatorFactory.Name = "PART_Indicator";
        indicatorFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        indicatorFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)));
        indicatorFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

        gridFactory.AppendChild(bgFactory);
        gridFactory.AppendChild(trackFactory);
        gridFactory.AppendChild(indicatorFactory);

        template.VisualTree = gridFactory;
        return template;
    }

    private static void SetBarColor(Border? indicator, double utilization)
    {
        if (indicator == null) return;
        var color = utilization >= 100 ? "#F44336"
            : utilization >= 90 ? "#9C27B0"
            : utilization >= 75 ? "#FF9800"
            : "#4CAF50";
        indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static Border? GetBarIndicator(ProgressBar bar)
    {
        bar.ApplyTemplate();
        return bar.Template.FindName("PART_Indicator", bar) as Border;
    }

    private static string FormatLabel(string apiLabel)
    {
        var parts = apiLabel.Split('-');
        return parts.Length >= 2 ? parts[^1].ToUpperInvariant() : apiLabel.ToUpperInvariant();
    }
}
