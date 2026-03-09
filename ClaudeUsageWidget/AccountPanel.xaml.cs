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

    private readonly List<(ProgressBar Bar, TextBlock Text, Grid Container)> _bars = [];

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
            var (bar, text, _) = _bars[i];

            bar.Value = limit.Utilization;
            SetBarColor(GetBarIndicator(bar), limit.Utilization);

            bool showText = data.Limits.Count <= 4;
            text.Text = showText
                ? $"{limit.Utilization:0}% {TimeFormatter.FormatResetTime(limit.ResetsAt)}"
                : "";
            text.Foreground = Brushes.White;

            bar.ToolTip = $"{FormatLabel(limit.Label)} {limit.Utilization:0}% Reset: {TimeFormatter.FormatResetTime(limit.ResetsAt)}";
        }
    }

    public void ShowLoadingState()
    {
        _isLoading = true;
        EnsureBarCount(2);
        foreach (var (bar, text, _) in _bars)
        {
            bar.Value = 0;
            bar.ToolTip = null;
            text.Text = SpinnerFrames[_spinnerFrame];
        }
    }

    public void AdvanceSpinner()
    {
        if (!_isLoading) return;
        _spinnerFrame = (_spinnerFrame + 1) % SpinnerFrames.Length;
        foreach (var (_, text, _) in _bars)
            text.Text = SpinnerFrames[_spinnerFrame];
    }

    public void ClearSpinner()
    {
        foreach (var (_, text, _) in _bars)
            text.Text = "";
    }

    public void ShowErrorState()
    {
        _isLoading = false;
        EnsureBarCount(Math.Max(_bars.Count, 2));
        var maroon = new SolidColorBrush(Colors.Maroon);
        foreach (var (bar, text, _) in _bars)
        {
            bar.Value = 100;
            bar.ToolTip = null;
            var ind = GetBarIndicator(bar);
            if (ind != null) ind.Background = maroon;
            text.Foreground = Brushes.White;
            text.Text = "Error";
        }
    }

    public void RefreshText(UsageData? lastUsage)
    {
        if (lastUsage == null) return;
        for (int i = 0; i < lastUsage.Limits.Count && i < _bars.Count; i++)
        {
            var limit = lastUsage.Limits[i];
            bool showText = lastUsage.Limits.Count <= 4;
            _bars[i].Text.Text = showText
                ? $"{limit.Utilization:0}% {TimeFormatter.FormatResetTime(limit.ResetsAt)}"
                : "";
            _bars[i].Bar.ToolTip = $"{FormatLabel(limit.Label)} {limit.Utilization:0}% Reset: {TimeFormatter.FormatResetTime(limit.ResetsAt)}";
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

        // Adjust spacing based on count
        double spacing = count <= 2 ? 5 : count <= 4 ? 3 : 2;
        for (int i = 0; i < _bars.Count; i++)
            _bars[i].Container.Margin = new Thickness(0, i == 0 ? 0 : spacing, 0, 0);
    }

    private static (ProgressBar Bar, TextBlock Text, Grid Container) CreateBarEntry()
    {
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0 };
        bar.Template = CreateBarTemplate();

        var text = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 9,
            Text = ""
        };

        var container = new Grid();
        container.Children.Add(bar);
        container.Children.Add(text);

        return (bar, text, container);
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
        var color = utilization < 75 ? "#4CAF50" : utilization < 90 ? "#FF9800" : "#F44336";
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
