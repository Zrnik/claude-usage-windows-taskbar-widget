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

    public void UpdateAndShowJira(JiraUsageData? data, string? errorMessage,
        double widgetLeft, double widgetTop)
    {
        LimitsPanel.Children.Clear();

        if (data != null)
            BuildJiraPopup(data);

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
        footerGrid.Children.Add(new TextBlock
        {
            Text = "JIRA",
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            FontSize = 8
        });
        var versionBlock = new TextBlock
        {
            Text = $"v{Updater.CurrentVersion}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            FontSize = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(versionBlock, 1);
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

    private void BuildJiraPopup(JiraUsageData data)
    {
        // Header
        LimitsPanel.Children.Add(new TextBlock
        {
            Text = $"JIRA · {data.ProjectKey}",
            Foreground = Brushes.Gray,
            FontSize = 9,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // My status breakdown
        int myTotal = data.MyByCategory.Values.Sum();
        int todo = data.MyByCategory.TryGetValue("new", out var n) ? n : 0;
        int inprog = data.MyByCategory.TryGetValue("indeterminate", out var i) ? i : 0;
        int done = data.MyByCategory.TryGetValue("done", out var d) ? d : 0;

        if (myTotal > 0)
        {
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = "MY ISSUES",
                Foreground = Brushes.Gray,
                FontSize = 9,
                Margin = new Thickness(0, 0, 0, 2)
            });
            LimitsPanel.Children.Add(MakeJiraRow("To Do", todo, Color.FromRgb(0x99, 0x99, 0x99)));
            LimitsPanel.Children.Add(MakeJiraRow("In Progress", inprog, Color.FromRgb(0xFF, 0x98, 0x00)));
            LimitsPanel.Children.Add(MakeJiraRow("Done", done, Color.FromRgb(0x4C, 0xAF, 0x50)));

            // Detailed status breakdown if there are non-standard statuses
            if (data.MyByStatus.Count > 0)
            {
                LimitsPanel.Children.Add(new TextBlock
                {
                    Text = "BY STATUS",
                    Foreground = Brushes.Gray,
                    FontSize = 9,
                    Margin = new Thickness(0, 6, 0, 2)
                });
                foreach (var kv in data.MyByStatus.OrderByDescending(p => p.Value))
                    LimitsPanel.Children.Add(MakeJiraRow(kv.Key, kv.Value,
                        Color.FromRgb(0xCC, 0xCC, 0xCC)));
            }

            LimitsPanel.Children.Add(new TextBlock
            {
                Text = $"Story points: {data.MyDoneStoryPoints:0.#} done / {data.MyStoryPoints:0.#} total",
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                FontSize = 9,
                Margin = new Thickness(0, 6, 0, 2)
            });
        }
        else
        {
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = "No issues assigned to you in this project",
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 9,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        // Active tasks
        if (data.MyActiveIssues.Count > 0)
        {
            AddSeparator();
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = $"ACTIVE TASKS ({data.MyActiveIssues.Count})",
                Foreground = Brushes.Gray,
                FontSize = 9,
                Margin = new Thickness(0, 0, 0, 2)
            });
            // Show up to 10 most recent active issues
            int shown = 0;
            foreach (var issue in data.MyActiveIssues)
            {
                if (shown >= 10) break;
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var statusColor = issue.StatusCategory switch
                {
                    "indeterminate" => Color.FromRgb(0xFF, 0x98, 0x00),
                    "new" => Color.FromRgb(0x88, 0x88, 0x88),
                    _ => Color.FromRgb(0xCC, 0xCC, 0xCC)
                };
                var keyBlock = new TextBlock
                {
                    Text = issue.Key,
                    Foreground = new SolidColorBrush(statusColor),
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                Grid.SetColumn(keyBlock, 0);

                var summaryBlock = new TextBlock
                {
                    Text = issue.Summary,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    FontSize = 9,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 160
                };
                Grid.SetColumn(summaryBlock, 1);

                var statusBlock = new TextBlock
                {
                    Text = issue.StoryPoints > 0 ? $"{issue.StatusName} · {issue.StoryPoints:0.#} SP" : issue.StatusName,
                    Foreground = new SolidColorBrush(statusColor),
                    FontSize = 8,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                Grid.SetColumn(statusBlock, 2);

                row.Children.Add(keyBlock);
                row.Children.Add(summaryBlock);
                row.Children.Add(statusBlock);
                LimitsPanel.Children.Add(row);
                shown++;
            }
            if (data.MyActiveIssues.Count > 10)
            {
                LimitsPanel.Children.Add(new TextBlock
                {
                    Text = $"+ {data.MyActiveIssues.Count - 10} more",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 8,
                    FontStyle = FontStyles.Italic
                });
            }
        }

        // Trend charts (require history)
        var history = JiraHistoryStore.Instance.GetLastDays(30);
        if (history.Count >= 2 && data.Me != null)
        {
            AddSeparator();
            BuildJiraTrendsBlock(history, data);
        }

        // Developer ranking
        if (data.DeveloperRanking.Count > 1)
        {
            AddSeparator();
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = "RANKING (by SP done)",
                Foreground = Brushes.Gray,
                FontSize = 9,
                Margin = new Thickness(0, 0, 0, 2)
            });

            int rank = 1;
            foreach (var dev in data.DeveloperRanking)
            {
                bool isMe = data.Me != null && dev.AccountId == data.Me.AccountId;
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var rankColor = isMe ? Color.FromRgb(0xFF, 0xC1, 0x07) : Color.FromRgb(0x88, 0x88, 0x88);
                var rankBlock = new TextBlock
                {
                    Text = $"#{rank}",
                    Foreground = new SolidColorBrush(rankColor),
                    FontSize = 9,
                    FontWeight = isMe ? FontWeights.SemiBold : FontWeights.Normal
                };
                Grid.SetColumn(rankBlock, 0);

                var nameBlock = new TextBlock
                {
                    Text = isMe ? dev.DisplayName + " (you)" : dev.DisplayName,
                    Foreground = new SolidColorBrush(isMe ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    FontSize = 9,
                    FontWeight = isMe ? FontWeights.SemiBold : FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 170
                };
                Grid.SetColumn(nameBlock, 1);

                var statBlock = new TextBlock
                {
                    Text = $"{dev.DoneStoryPoints:0.#} SP · {dev.DoneIssues}/{dev.TotalIssues}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    FontSize = 9
                };
                Grid.SetColumn(statBlock, 2);

                row.Children.Add(rankBlock);
                row.Children.Add(nameBlock);
                row.Children.Add(statBlock);
                LimitsPanel.Children.Add(row);
                rank++;
            }
        }
    }

    private void BuildJiraTrendsBlock(IReadOnlyList<JiraHistoryRecord> history, JiraUsageData current)
    {
        // Compute done-per-day from snapshots: today_done - yesterday_done (clamped to 0)
        var sorted = history.OrderBy(r => r.Date, StringComparer.Ordinal).ToList();
        var donePerDay = new List<(DateTimeOffset Date, int Delta)>();
        for (int i = 1; i < sorted.Count; i++)
        {
            if (!DateTimeOffset.TryParse(sorted[i].Date, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeLocal, out var d)) continue;
            int delta = Math.Max(0, sorted[i].MyDoneIssues - sorted[i - 1].MyDoneIssues);
            donePerDay.Add((d, delta));
        }
        // Velocity: average of done-per-day over last 7 days  +  last 28 days
        int velocity7 = donePerDay.Where(p => p.Date >= DateTimeOffset.Now.AddDays(-7)).Sum(p => p.Delta);
        int velocity28 = donePerDay.Where(p => p.Date >= DateTimeOffset.Now.AddDays(-28)).Sum(p => p.Delta);
        double weekly7 = velocity7;
        double weekly28 = velocity28 / 4.0;

        // Streak: consecutive days back from today with at least one done
        int streak = 0;
        var today = DateTimeOffset.Now.Date;
        var byDate = donePerDay.ToDictionary(p => p.Date.Date, p => p.Delta);
        for (int back = 0; back < 60; back++)
        {
            var d = today.AddDays(-back);
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
            if (byDate.TryGetValue(d, out var dv) && dv > 0) streak++;
            else if (back > 0) break;
        }

        LimitsPanel.Children.Add(new TextBlock
        {
            Text = "TRENDS",
            Foreground = Brushes.Gray,
            FontSize = 9,
            Margin = new Thickness(0, 0, 0, 2)
        });

        // Velocity row
        LimitsPanel.Children.Add(new TextBlock
        {
            Text = $"Velocity: {weekly7:0.#}/wk (last 7d) · {weekly28:0.#}/wk (4w avg)",
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            FontSize = 9,
            Margin = new Thickness(0, 0, 0, 2)
        });

        // Streak row
        if (streak > 0)
        {
            var streakColor = streak >= 5
                ? Color.FromRgb(0xFF, 0xC1, 0x07)
                : streak >= 3
                    ? Color.FromRgb(0x4C, 0xAF, 0x50)
                    : Color.FromRgb(0xCC, 0xCC, 0xCC);
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = $"🔥 Streak: {streak} working day{(streak == 1 ? "" : "s")} with ≥1 done",
                Foreground = new SolidColorBrush(streakColor),
                FontSize = 9,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        // Done-per-day chart
        LimitsPanel.Children.Add(new TextBlock
        {
            Text = "Done per day (last 30d)",
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            FontSize = 8,
            Margin = new Thickness(0, 2, 0, 1)
        });
        LimitsPanel.Children.Add(BuildJiraDoneChart(donePerDay));

        // Ranking trend sparkline
        if (current.Me != null && history.Any(h => h.RankingSize > 1))
        {
            LimitsPanel.Children.Add(new TextBlock
            {
                Text = "Rank trend (lower = better)",
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 8,
                Margin = new Thickness(0, 4, 0, 1)
            });
            LimitsPanel.Children.Add(BuildJiraRankChart(sorted, current.Me.AccountId));
        }
    }

    private static UIElement BuildJiraDoneChart(List<(DateTimeOffset Date, int Delta)> data)
    {
        var canvas = new Canvas { Height = 40, Background = Brushes.Transparent };
        canvas.SizeChanged += (_, _) => RenderJiraDoneChart(canvas, data);
        canvas.Loaded += (_, _) => RenderJiraDoneChart(canvas, data);
        return canvas;
    }

    private static void RenderJiraDoneChart(Canvas canvas, List<(DateTimeOffset Date, int Delta)> data)
    {
        canvas.Children.Clear();
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0 || data.Count == 0) return;

        const double padX = 2, padY = 2;
        var startDate = DateTimeOffset.Now.Date.AddDays(-29);
        var totalDays = 30;

        int maxVal = Math.Max(1, data.Max(p => p.Delta));
        double colW = (w - 2 * padX) / totalDays;

        for (int i = 0; i < totalDays; i++)
        {
            var d = startDate.AddDays(i);
            var match = data.FirstOrDefault(p => p.Date.Date == d);
            int val = match.Date == default ? 0 : match.Delta;
            if (val <= 0) continue;
            double barH = (h - 2 * padY) * val / maxVal;
            var color = val >= 3 ? Color.FromRgb(0x4C, 0xAF, 0x50)
                : val >= 1 ? Color.FromRgb(0x21, 0x96, 0xF3)
                : Color.FromRgb(0x88, 0x88, 0x88);
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = Math.Max(1, colW - 1),
                Height = barH,
                Fill = new SolidColorBrush(color),
                RadiusX = 1, RadiusY = 1
            };
            Canvas.SetLeft(rect, padX + i * colW);
            Canvas.SetTop(rect, h - padY - barH);
            canvas.Children.Add(rect);
        }
    }

    private static UIElement BuildJiraRankChart(List<JiraHistoryRecord> history, string myAccountId)
    {
        var canvas = new Canvas { Height = 30, Background = Brushes.Transparent };
        canvas.SizeChanged += (_, _) => RenderJiraRankChart(canvas, history, myAccountId);
        canvas.Loaded += (_, _) => RenderJiraRankChart(canvas, history, myAccountId);
        return canvas;
    }

    private static void RenderJiraRankChart(Canvas canvas, List<JiraHistoryRecord> history, string myAccountId)
    {
        canvas.Children.Clear();
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double padX = 2, padY = 2;
        var ranks = history
            .Where(r => r.RankingSize > 0 && r.MyRank > 0)
            .Select(r => (Date: DateTimeOffset.Parse(r.Date, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal), Rank: r.MyRank, Total: r.RankingSize))
            .OrderBy(r => r.Date)
            .ToList();
        if (ranks.Count < 2) return;

        var minDate = ranks.First().Date;
        var maxDate = ranks.Last().Date;
        double spanSecs = Math.Max(1, (maxDate - minDate).TotalSeconds);
        int maxRank = Math.Max(1, ranks.Max(r => r.Total));

        var points = new System.Windows.Media.PointCollection();
        foreach (var r in ranks)
        {
            double x = padX + (r.Date - minDate).TotalSeconds / spanSecs * (w - 2 * padX);
            // Invert Y: rank 1 is best (top)
            double y = padY + (r.Rank - 1.0) / Math.Max(1, maxRank - 1) * (h - 2 * padY);
            points.Add(new Point(x, y));
        }

        // Reference line at rank=1 (top)
        canvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = padX, X2 = w - padX, Y1 = padY, Y2 = padY,
            Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xC1, 0x07)),
            StrokeThickness = 0.5,
            StrokeDashArray = new DoubleCollection { 3, 3 }
        });

        var line = new System.Windows.Shapes.Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
            StrokeThickness = 1.5,
            Fill = null
        };
        canvas.Children.Add(line);

        // Highlight latest point
        var last = points[^1];
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 4, Height = 4,
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07))
        };
        Canvas.SetLeft(dot, last.X - 2);
        Canvas.SetTop(dot, last.Y - 2);
        canvas.Children.Add(dot);
    }

    private static UIElement MakeJiraRow(string label, int count, Color labelColor)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(labelColor),
            FontSize = 9
        };
        Grid.SetColumn(labelBlock, 0);

        var countBlock = new TextBlock
        {
            Text = count.ToString(),
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(countBlock, 1);

        row.Children.Add(labelBlock);
        row.Children.Add(countBlock);
        return row;
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
