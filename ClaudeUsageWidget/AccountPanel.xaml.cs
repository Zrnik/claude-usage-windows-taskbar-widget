using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudeUsageWidgetProvider;

public partial class AccountPanel : UserControl
{
    private static readonly string[] SpinnerFrames = ["|", "/", "—", "\\"];
    private int _spinnerFrame;

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
        Text5h.Foreground = Brushes.White;
        Text7d.Foreground = Brushes.White;

        var first = data.Limits.ElementAtOrDefault(0);
        var second = data.Limits.ElementAtOrDefault(1);

        if (first != null)
        {
            Bar5h.Value = first.Utilization;
            Text5h.Text = $"{first.Utilization:0}%  {TimeFormatter.FormatResetTime(first.ResetsAt)}";
            SetBarColor(GetBarIndicator(Bar5h), first.Utilization);
        }

        if (second != null)
        {
            Bar7d.Value = second.Utilization;
            Text7d.Text = $"{second.Utilization:0}%  {TimeFormatter.FormatResetTime(second.ResetsAt)}";
            SetBarColor(GetBarIndicator(Bar7d), second.Utilization);
        }
    }

    public void ShowLoadingState()
    {
        Bar5h.Value = 0; Bar7d.Value = 0;
        Text5h.Text = SpinnerFrames[_spinnerFrame];
        Text7d.Text = SpinnerFrames[_spinnerFrame];
    }

    public void AdvanceSpinner()
    {
        _spinnerFrame = (_spinnerFrame + 1) % SpinnerFrames.Length;
        Text5h.Text = SpinnerFrames[_spinnerFrame];
        Text7d.Text = SpinnerFrames[_spinnerFrame];
    }

    public void ClearSpinner()
    {
        Text5h.Text = "";
        Text7d.Text = "";
    }

    public void ShowErrorState()
    {
        Bar5h.Value = 100; Bar7d.Value = 100;
        var maroon = new SolidColorBrush(Colors.Maroon);
        var ind5h = GetBarIndicator(Bar5h);
        var ind7d = GetBarIndicator(Bar7d);
        if (ind5h != null) ind5h.Background = maroon;
        if (ind7d != null) ind7d.Background = maroon;
        Text5h.Foreground = Brushes.White; Text7d.Foreground = Brushes.White;
        Text5h.Text = "Error"; Text7d.Text = "Error";
    }

    public void RefreshText(UsageData? lastUsage)
    {
        if (lastUsage == null) return;
        var first = lastUsage.Limits.ElementAtOrDefault(0);
        var second = lastUsage.Limits.ElementAtOrDefault(1);
        if (first != null)
            Text5h.Text = $"{first.Utilization:0}%  {TimeFormatter.FormatResetTime(first.ResetsAt)}";
        if (second != null)
            Text7d.Text = $"{second.Utilization:0}%  {TimeFormatter.FormatResetTime(second.ResetsAt)}";
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
}
