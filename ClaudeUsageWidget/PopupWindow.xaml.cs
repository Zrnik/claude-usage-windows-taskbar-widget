using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ClaudeUsageWidgetProvider;

public partial class PopupWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private static readonly IntPtr HwndTopmost = new(-1);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    public PopupWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    public void UpdateAndShow(UsageData? data, string? errorMessage, string credentialPath,
        double widgetLeft, double widgetTop, string? accountKey = null)
    {
        LimitsPanel.Children.Clear();

        if (data != null)
        {
            foreach (var limit in data.Limits)
            {
                var labelText = FormatLabel(limit.Label);
                if (limit.Label == "spend" && data.SpendUsed.HasValue && data.SpendLimit.HasValue)
                    labelText += $"  ${data.SpendUsed:0.00} / ${data.SpendLimit:0.00}";

                var label = new TextBlock
                {
                    Text = labelText,
                    Foreground = Brushes.Gray,
                    FontSize = 9,
                    Margin = new Thickness(0, 0, 0, 2)
                };

                var barContainer = new Grid { Height = 12, Margin = new Thickness(0, 0, 0, 2) };
                var track = new Border { Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), CornerRadius = new CornerRadius(2) };
                var fill = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(2),
                    Background = GetBarBrush(limit.Utilization)
                };
                fill.SetBinding(WidthProperty, new System.Windows.Data.Binding("ActualWidth")
                {
                    Source = barContainer,
                    Converter = new PercentWidthConverter(),
                    ConverterParameter = limit.Utilization
                });
                var pctOverlay = new TextBlock
                {
                    Text = $"{limit.Utilization:0}%",
                    Foreground = Brushes.White,
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                barContainer.Children.Add(track);
                barContainer.Children.Add(fill);
                barContainer.Children.Add(pctOverlay);

                var resetGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var countdown = new TextBlock
                {
                    Text = $"Reset: {TimeFormatter.FormatResetTime(limit.ResetsAt)}",
                    Foreground = Brushes.LightGray,
                    FontSize = 9
                };
                Grid.SetColumn(countdown, 0);

                var resetDate = new TextBlock
                {
                    Text = limit.ResetsAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    Foreground = Brushes.Gray,
                    FontSize = 9
                };
                Grid.SetColumn(resetDate, 1);

                resetGrid.Children.Add(countdown);
                resetGrid.Children.Add(resetDate);

                var chart = new HistoryChart { Margin = new Thickness(0, 2, 0, 2) };
                if (accountKey != null)
                {
                    var history = UsageHistoryStore.Instance.GetHistory(accountKey);
                    chart.SetData(history, limit.Label);
                }

                LimitsPanel.Children.Add(label);
                LimitsPanel.Children.Add(barContainer);
                LimitsPanel.Children.Add(resetGrid);
                LimitsPanel.Children.Add(chart);

                var prediction = UsagePrediction.Predict(accountKey, limit);
                if (prediction != null)
                {
                    var predColor = prediction.Kind switch
                    {
                        PredictionResult.PredictionKind.LimitReached => Color.FromRgb(0xF4, 0x43, 0x36),
                        PredictionResult.PredictionKind.Approaching => Color.FromRgb(0xFF, 0x98, 0x00),
                        _ => Color.FromRgb(0x88, 0x88, 0x88)
                    };
                    LimitsPanel.Children.Add(new TextBlock
                    {
                        Text = prediction.Format(),
                        Foreground = new SolidColorBrush(predColor),
                        FontSize = 9,
                        Margin = new Thickness(0, 0, 0, 6)
                    });
                }
                else
                {
                    // Keep spacing consistent when no prediction
                    chart.Margin = new Thickness(0, 2, 0, 6);
                }
            }
        }

        if (errorMessage != null)
        {
            if (LimitsPanel.Children.Count > 0)
                AddSeparator();
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = errorMessage,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 260
            });
        }

        {
            AddSeparator();
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (!string.IsNullOrEmpty(credentialPath))
            {
                var pathBlock = new TextBlock
                {
                    Text = credentialPath,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 8,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 220
                };
                Grid.SetColumn(pathBlock, 0);
                footerGrid.Children.Add(pathBlock);
            }

            var versionBlock = new TextBlock
            {
                Text = $"v{Updater.CurrentVersion}",
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                FontSize = 8,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(versionBlock, 1);
            footerGrid.Children.Add(versionBlock);

            LimitsPanel.Children.Add(footerGrid);
        }

        // Remove trailing margin from last data item
        if (data != null && LimitsPanel.Children.Count > 0 && LimitsPanel.Children[^1] is FrameworkElement last)
            last.Margin = new Thickness(0);

        UpdateLayout();

        Left = widgetLeft;
        Top = widgetTop - ActualHeight - 4;

        // EnsureHandle creates HWND without showing, so we can set TOPMOST before the window is visible
        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        var hwnd = helper.Handle;
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        Show();
    }

    private void AddSeparator() =>
        LimitsPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
            Margin = new Thickness(0, 4, 0, 4)
        });

    private static string FormatLabel(string apiLabel)
    {
        var parts = apiLabel.Split('-');
        return parts.Length >= 2 ? parts[^1].ToUpperInvariant() : apiLabel.ToUpperInvariant();
    }

    private static SolidColorBrush GetBarBrush(double utilization) =>
        new(utilization >= 100 ? Color.FromRgb(0xF4, 0x43, 0x36)
            : utilization >= 90 ? Color.FromRgb(0x9C, 0x27, 0xB0)
            : utilization >= 75 ? Color.FromRgb(0xFF, 0x98, 0x00)
            : Color.FromRgb(0x4C, 0xAF, 0x50));
}

internal class PercentWidthConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double width && parameter is double pct)
            return Math.Max(0, Math.Min(width, width * Math.Min(pct, 100.0) / 100.0));
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
