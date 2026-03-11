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
        ServiceIcon.Source = new BitmapImage(new Uri(
            service == ServiceType.Claude
                ? "pack://application:,,,/Assets/claude-logo.png"
                : "pack://application:,,,/Assets/codex-logo.png"));
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
