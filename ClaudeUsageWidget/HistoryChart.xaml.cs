using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClaudeUsageWidgetProvider;

public partial class HistoryChart : UserControl
{
    private const double PadX = 2.0;
    private const double PadY = 2.0;

    private static readonly TimeSpan GapThreshold = TimeSpan.FromHours(2);

    private List<(DateTimeOffset Ts, double Value)>? _points;
    private TimeSpan _timeWindow = TimeSpan.FromDays(14);

    private enum SegmentColor { Green, Orange, Purple, OverLimit }
    private record ColorSegment(SegmentColor Color, List<int> Indices);

    public HistoryChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RenderChart();
    }

    internal void SetData(IReadOnlyList<HistoryRecord> records, string label)
    {
        _timeWindow = GetTimeWindow(label);
        var cutoff = DateTimeOffset.UtcNow - _timeWindow;

        _points = records
            .Where(r => r.Limits.ContainsKey(label))
            .Select(r => (
                Ts: DateTimeOffset.Parse(r.Timestamp, CultureInfo.InvariantCulture),
                Value: r.Limits[label]))
            .Where(p => p.Ts >= cutoff)
            .OrderBy(p => p.Ts)
            .ToList();

        if (IsLoaded && ActualWidth > 0)
            RenderChart();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (ActualWidth > 0)
            RenderChart();
    }

    private void RenderChart()
    {
        ChartCanvas.Children.Clear();
        if (_points == null || _points.Count < 2)
            return;

        double w = ChartCanvas.ActualWidth;
        double h = ChartCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        var windowEnd = DateTimeOffset.UtcNow;
        var windowStart = windowEnd - _timeWindow;
        var windowSpanSeconds = (windowEnd - windowStart).TotalSeconds;

        var processed = ProcessGaps(_points);

        double maxValue = 100.0;
        foreach (var p in processed)
            if (p.Value > maxValue) maxValue = p.Value;
        // Round up to nearest 10 for clean Y axis (e.g. 137% -> 140%)
        if (maxValue > 100.0)
            maxValue = Math.Ceiling(maxValue / 10.0) * 10.0;

        var segments = BuildColorSegments(processed);

        foreach (var seg in segments)
        {
            if (seg.Indices.Count < 1)
                continue;

            var color = GetColor(seg.Color);
            var points = new PointCollection();

            foreach (var i in seg.Indices)
            {
                double x = TimestampToX(processed[i].Ts, windowStart, windowSpanSeconds, w);
                double y = PadY + (1.0 - processed[i].Value / maxValue) * (h - 2 * PadY);
                points.Add(new Point(x, y));
            }

            // Fill polygon
            var fillPoints = new PointCollection(points);
            fillPoints.Add(new Point(points[^1].X, h - PadY));
            fillPoints.Add(new Point(points[0].X, h - PadY));

            var polygon = new Polygon
            {
                Points = fillPoints,
                Fill = new SolidColorBrush(color) { Opacity = 0.20 },
                Stroke = null
            };
            ChartCanvas.Children.Add(polygon);

            // Line
            var line = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.5,
                Fill = null
            };
            ChartCanvas.Children.Add(line);
        }

        // Draw reference lines every 25%
        for (double pct = 25; pct <= maxValue; pct += 25)
        {
            double lineY = PadY + (1.0 - pct / maxValue) * (h - 2 * PadY);
            bool is100 = Math.Abs(pct - 100.0) < 0.1;
            var refLine = new Line
            {
                X1 = PadX,
                Y1 = lineY,
                X2 = w - PadX,
                Y2 = lineY,
                Stroke = new SolidColorBrush(Color.FromArgb(
                    (byte)(is100 ? 0x80 : 0x30), 0xFF, 0xFF, 0xFF)),
                StrokeThickness = is100 ? 0.5 : 0.5,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            ChartCanvas.Children.Add(refLine);
        }
    }

    private static List<(DateTimeOffset Ts, double Value)> ProcessGaps(
        List<(DateTimeOffset Ts, double Value)> raw)
    {
        if (raw.Count == 0) return raw;

        var result = new List<(DateTimeOffset Ts, double Value)>();
        for (int i = 0; i < raw.Count; i++)
        {
            result.Add(raw[i]);
            if (i < raw.Count - 1)
            {
                var gap = raw[i + 1].Ts - raw[i].Ts;
                if (gap >= GapThreshold)
                {
                    // Hold last known value until just before next real point
                    result.Add((raw[i + 1].Ts.AddSeconds(-1), raw[i].Value));
                }
            }
        }
        return result;
    }

    private static double TimestampToX(DateTimeOffset ts, DateTimeOffset windowStart,
        double windowSpanSeconds, double chartWidth)
    {
        var offset = (ts - windowStart).TotalSeconds;
        return PadX + (offset / windowSpanSeconds) * (chartWidth - 2 * PadX);
    }

    private static List<ColorSegment> BuildColorSegments(List<(DateTimeOffset Ts, double Value)> points)
    {
        var segments = new List<ColorSegment>();
        if (points.Count == 0)
            return segments;

        var currentColor = Classify(points[0].Value);
        var currentIndices = new List<int> { 0 };

        for (int i = 1; i < points.Count; i++)
        {
            var c = Classify(points[i].Value);
            if (c == currentColor)
            {
                currentIndices.Add(i);
            }
            else
            {
                // Add boundary point to current segment
                currentIndices.Add(i);
                segments.Add(new ColorSegment(currentColor, currentIndices));

                // Start new segment with boundary point
                currentColor = c;
                currentIndices = new List<int> { i };
            }
        }
        segments.Add(new ColorSegment(currentColor, currentIndices));
        return segments;
    }

    private static SegmentColor Classify(double value) =>
        value >= 100.0 ? SegmentColor.OverLimit :
        value >= 90.0 ? SegmentColor.Purple :
        value >= 75.0 ? SegmentColor.Orange :
        SegmentColor.Green;

    private static Color GetColor(SegmentColor seg) => seg switch
    {
        SegmentColor.Green => Color.FromRgb(0x4C, 0xAF, 0x50),
        SegmentColor.Orange => Color.FromRgb(0xFF, 0x98, 0x00),
        SegmentColor.Purple => Color.FromRgb(0x9C, 0x27, 0xB0),
        SegmentColor.OverLimit => Color.FromRgb(0xF4, 0x43, 0x36),
        _ => Color.FromRgb(0x4C, 0xAF, 0x50)
    };

    public static string TimeWindowLabel(string label)
    {
        var window = GetTimeWindow(label);
        return window.TotalDays >= 1 ? $"{window.TotalDays:0}d" : $"{window.TotalHours:0}h";
    }

    public static TimeSpan GetTimeWindow(string label)
    {
        var overrides = SettingsStore.Instance.ChartWindowDays;
        if (overrides.TryGetValue(label, out var days))
            return TimeSpan.FromDays(days);
        return DefaultTimeWindow(label);
    }

    public static int GetDefaultDays(string label) => (int)DefaultTimeWindow(label).TotalDays;

    private static TimeSpan DefaultTimeWindow(string label)
    {
        if (label.Contains("5h", StringComparison.OrdinalIgnoreCase))
            return TimeSpan.FromDays(2);
        if (label.Contains("7d", StringComparison.OrdinalIgnoreCase))
            return TimeSpan.FromDays(14);
        if (label.Contains("session", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("100h", StringComparison.OrdinalIgnoreCase))
            return TimeSpan.FromDays(14);
        if (label.Contains("review", StringComparison.OrdinalIgnoreCase))
            return TimeSpan.FromDays(7);
        return TimeSpan.FromDays(14);
    }
}
