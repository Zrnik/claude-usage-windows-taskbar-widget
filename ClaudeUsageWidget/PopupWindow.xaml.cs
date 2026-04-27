using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

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

                var chartLabel = new TextBlock
                {
                    Text = $"History ({HistoryChart.TimeWindowLabel(limit.Label)})",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 8,
                    Margin = new Thickness(0, 4, 0, 0)
                };

                LimitsPanel.Children.Add(label);
                LimitsPanel.Children.Add(barContainer);
                LimitsPanel.Children.Add(resetGrid);
                LimitsPanel.Children.Add(chartLabel);
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

    public void UpdateAndShowToggl(TogglUsageData? data, string? errorMessage,
        double widgetLeft, double widgetTop)
    {
        LimitsPanel.Children.Clear();

        if (data != null)
        {
            BuildTogglPopup(data);
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

        AddSeparator();
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var source = new TextBlock
        {
            Text = "Toggl Track",
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            FontSize = 8
        };
        Grid.SetColumn(source, 0);
        var versionBlock = new TextBlock
        {
            Text = $"v{Updater.CurrentVersion}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            FontSize = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(versionBlock, 1);
        footerGrid.Children.Add(source);
        footerGrid.Children.Add(versionBlock);
        LimitsPanel.Children.Add(footerGrid);

        UpdateLayout();
        Left = widgetLeft;
        Top = widgetTop - ActualHeight - 4;

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        SetWindowPos(helper.Handle, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        Show();
    }

    private void BuildTogglPopup(TogglUsageData data)
    {
        var now = DateTimeOffset.Now;
        var monthStart = data.MonthStart;
        var monthEnd = data.MonthResetsAt;
        int wdTotal = Pace.CountWorkingDays(monthStart, monthEnd.AddDays(-1));
        int wdElapsed = Pace.CountWorkingDays(monthStart, now);
        double wdRemainingFractional = Pace.WorkingDaysRemainingFractional(now, monthEnd);
        int wdRemainingDisplay = (int)Math.Ceiling(wdRemainingFractional);

        double targetPerDay = wdTotal > 0 ? data.TargetCzk / wdTotal : 0;
        double expectedSoFar = targetPerDay * wdElapsed;
        double delta = data.EarnedCzk - expectedSoFar;
        double deltaDays = targetPerDay > 0 ? delta / targetPerDay : 0;
        double remaining = Math.Max(0, data.TargetCzk - data.EarnedCzk);
        double impliedRate = (wdTotal > 0 && data.TargetCzk > 0) ? data.TargetCzk / (wdTotal * 8.0) : 0;
        double requiredHoursPerDay = (impliedRate > 0 && wdRemainingFractional > 0)
            ? remaining / (impliedRate * wdRemainingFractional) : 0;
        double pct = data.TargetCzk > 0 ? Math.Min(100.0, data.EarnedCzk / data.TargetCzk * 100.0) : 0;

        // Header
        LimitsPanel.Children.Add(new TextBlock
        {
            Text = "TOGGL TRACK",
            Foreground = Brushes.Gray,
            FontSize = 9,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Progress bar same style as Claude popup
        var barContainer = new Grid { Height = 14, Margin = new Thickness(0, 0, 0, 4) };
        var track = new Border { Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), CornerRadius = new CornerRadius(2) };
        var fill = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(data.EarnedCzk >= data.TargetCzk && data.TargetCzk > 0
                ? Color.FromRgb(0x21, 0x96, 0xF3)
                : Color.FromRgb(0x4C, 0xAF, 0x50))
        };
        fill.SetBinding(WidthProperty, new System.Windows.Data.Binding("ActualWidth")
        {
            Source = barContainer,
            Converter = new PercentWidthConverter(),
            ConverterParameter = pct
        });
        var pctOverlay = new TextBlock
        {
            Text = $"{FormatCzk(data.EarnedCzk)} / {FormatCzk(data.TargetCzk)}  ({pct:0}%)",
            Foreground = Brushes.White,
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        barContainer.Children.Add(track);
        barContainer.Children.Add(fill);
        barContainer.Children.Add(pctOverlay);
        LimitsPanel.Children.Add(barContainer);

        // Pace block
        LimitsPanel.Children.Add(MakeLine(
            $"Plan: {FormatCzk(data.EarnedCzk)} / {FormatCzk(expectedSoFar)} expected",
            Brushes.LightGray));

        var deltaColor = delta >= 0
            ? Color.FromRgb(0x4C, 0xAF, 0x50)
            : deltaDays > -1
                ? Color.FromRgb(0xFF, 0x98, 0x00)
                : Color.FromRgb(0xF4, 0x43, 0x36);
        string deltaSign = delta >= 0 ? "+" : "";
        LimitsPanel.Children.Add(MakeLine(
            $"Delta: {deltaSign}{FormatCzk(delta)} ({deltaSign}{deltaDays:0.#}d)",
            new SolidColorBrush(deltaColor)));

        double perDayNeeded = wdRemainingFractional > 0 ? remaining / wdRemainingFractional : 0;
        LimitsPanel.Children.Add(MakeLine(
            $"Remaining: {wdRemainingDisplay} work days · {FormatCzk(perDayNeeded)}/day",
            Brushes.LightGray));

        if (requiredHoursPerDay > 0 && wdRemainingFractional > 0)
        {
            var hColor = requiredHoursPerDay <= 8
                ? Color.FromRgb(0x4C, 0xAF, 0x50)
                : requiredHoursPerDay <= 10
                    ? Color.FromRgb(0xFF, 0x98, 0x00)
                    : Color.FromRgb(0xF4, 0x43, 0x36);
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = $"Need: {requiredHoursPerDay:0.#} h/day (Mon–Fri)",
                Foreground = new SolidColorBrush(hColor),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 0, 1)
            });

            // Weekend-inclusive variant
            double calDaysFractional = Pace.CalendarDaysRemainingFractional(now, monthEnd);
            int calDaysDisplay = (int)Math.Ceiling(calDaysFractional);
            if (calDaysFractional > 0 && impliedRate > 0)
            {
                double reqHoursAllDays = remaining / (impliedRate * calDaysFractional);
                var hColor2 = reqHoursAllDays <= 8
                    ? Color.FromRgb(0x4C, 0xAF, 0x50)
                    : reqHoursAllDays <= 10
                        ? Color.FromRgb(0xFF, 0x98, 0x00)
                        : Color.FromRgb(0xF4, 0x43, 0x36);
                LimitsPanel.Children.Add(new TextBlock
                {
                    Text = $"Need: {reqHoursAllDays:0.#} h/day (incl. weekends, {calDaysDisplay}d)",
                    Foreground = new SolidColorBrush(hColor2),
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
        }
        else if (remaining <= 0 && data.TargetCzk > 0)
        {
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = "✓ Target reached",
                Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 0, 4)
            });
        }

        // Breakdown per project
        if (data.Breakdown.Count > 0)
        {
            AddSeparator();
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = "PROJECTS",
                Foreground = Brushes.Gray,
                FontSize = 9,
                Margin = new Thickness(0, 0, 0, 2)
            });

            foreach (var p in data.Breakdown)
            {
                var label = p.ClientName != null
                    ? $"{p.ClientName} / {p.ProjectName}"
                    : p.ProjectName;
                var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var left = new TextBlock
                {
                    Text = label,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    FontSize = 9,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 170
                };
                Grid.SetColumn(left, 0);

                var rateStr = p.RateCzk > 0
                    ? $"{p.Hours:0.#}h × {p.RateCzk:0} = {FormatCzk(p.Earned)}"
                    : $"{p.Hours:0.#}h (no rate)";
                var right = new TextBlock
                {
                    Text = rateStr,
                    Foreground = new SolidColorBrush(p.RateCzk > 0
                        ? Color.FromRgb(0xCC, 0xCC, 0xCC)
                        : Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 9
                };
                Grid.SetColumn(right, 1);

                grid.Children.Add(left);
                grid.Children.Add(right);
                LimitsPanel.Children.Add(grid);
            }
        }

        // Cumulative chart
        AddSeparator();
        LimitsPanel.Children.Add(new TextBlock
        {
            Text = "MONTH PROGRESS",
            Foreground = Brushes.Gray,
            FontSize = 9,
            Margin = new Thickness(0, 0, 0, 2)
        });
        LimitsPanel.Children.Add(BuildCumulativeChart(data, monthStart, monthEnd));
    }

    private static TextBlock MakeLine(string text, Brush fg) => new()
    {
        Text = text,
        Foreground = fg,
        FontSize = 9,
        Margin = new Thickness(0, 1, 0, 1)
    };

    private static string FormatCzk(double czk)
    {
        if (double.IsNaN(czk) || double.IsInfinity(czk)) return "— Kč";
        // Czech style: "50 000 Kč" with non-breaking space as thousand separator, no decimals
        var nfi = (System.Globalization.NumberFormatInfo)System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
        nfi.NumberGroupSeparator = " "; // non-breaking space
        nfi.NumberDecimalDigits = 0;
        return $"{czk.ToString("N", nfi)} Kč";
    }

    private static UIElement BuildCumulativeChart(TogglUsageData current, DateTimeOffset monthStart, DateTimeOffset monthEnd)
    {
        var canvas = new Canvas { Height = 50, Background = Brushes.Transparent };
        canvas.SizeChanged += (_, _) => RenderCumulativeChart(canvas, current, monthStart, monthEnd);
        // Trigger once on Loaded if SizeChanged hasn't fired yet
        canvas.Loaded += (_, _) => RenderCumulativeChart(canvas, current, monthStart, monthEnd);
        return canvas;
    }

    private static void RenderCumulativeChart(Canvas canvas, TogglUsageData current,
        DateTimeOffset monthStart, DateTimeOffset monthEnd)
    {
        canvas.Children.Clear();
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double target = Math.Max(current.TargetCzk, current.EarnedCzk);
        if (target <= 0) return;

        int totalDays = (int)Math.Round((monthEnd - monthStart).TotalDays);
        if (totalDays <= 0) return;

        // Ideal pace line (0 at day 0, target at last day)
        const double padX = 2, padY = 2;
        double chartW = w - 2 * padX;
        double chartH = h - 2 * padY;

        double MapY(double value) => padY + (1.0 - value / target) * chartH;
        double MapX(double dayOffset) => padX + (dayOffset / totalDays) * chartW;

        // Reference line at 100%
        canvas.Children.Add(new Line
        {
            X1 = padX, X2 = w - padX,
            Y1 = MapY(current.TargetCzk), Y2 = MapY(current.TargetCzk),
            Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 0.5,
            StrokeDashArray = new DoubleCollection { 4, 3 }
        });

        // Ideal pace (diagonal from 0,0 bottom-left to target,lastDay top-right)
        canvas.Children.Add(new Line
        {
            X1 = MapX(0), Y1 = MapY(0),
            X2 = MapX(totalDays), Y2 = MapY(current.TargetCzk),
            Stroke = new SolidColorBrush(Color.FromArgb(0x66, 0xAA, 0xAA, 0xAA)),
            StrokeThickness = 1.0,
            StrokeDashArray = new DoubleCollection { 3, 3 }
        });

        // Actual cumulative curve — built from Toggl time entries (per-day aggregated), so it works
        // independently of widget uptime / history snapshots.
        var points = new PointCollection { new Point(MapX(0), MapY(0)) };
        double cumulative = 0;
        var sorted = current.DailyBreakdown.OrderBy(d => d.Date).ToList();
        foreach (var day in sorted)
        {
            double dayOffset = (day.Date - monthStart).TotalDays;
            if (dayOffset < 0) continue;
            if (dayOffset > totalDays) dayOffset = totalDays;
            // Start-of-day baseline (holds previous total)
            points.Add(new Point(MapX(dayOffset), MapY(cumulative)));
            cumulative += day.EarnedCzk;
            // End-of-day value (after adding today's earnings)
            points.Add(new Point(MapX(dayOffset + 1), MapY(cumulative)));
        }

        // Ensure latest point reflects current live value at 'now' (handles intra-day updates)
        var now = DateTimeOffset.Now;
        double nowOffset = (now - monthStart).TotalDays;
        if (nowOffset >= 0 && nowOffset <= totalDays && current.EarnedCzk > 0)
        {
            var nowPoint = new Point(MapX(nowOffset), MapY(current.EarnedCzk));
            if (points.Count == 0 || points[^1].X < nowPoint.X)
                points.Add(nowPoint);
            else
                points[^1] = nowPoint;
        }

        if (points.Count >= 2)
        {
            var lineColor = current.EarnedCzk >= current.TargetCzk && current.TargetCzk > 0
                ? Color.FromRgb(0x21, 0x96, 0xF3)
                : Color.FromRgb(0x4C, 0xAF, 0x50);

            // Fill under curve
            var fillPoints = new PointCollection(points)
            {
                new Point(points[^1].X, MapY(0)),
                new Point(points[0].X, MapY(0))
            };
            canvas.Children.Add(new Polygon
            {
                Points = fillPoints,
                Fill = new SolidColorBrush(lineColor) { Opacity = 0.20 },
                Stroke = null
            });
            canvas.Children.Add(new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(lineColor),
                StrokeThickness = 1.5,
                Fill = null
            });
        }
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
