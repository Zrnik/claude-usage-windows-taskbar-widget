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

    private enum SegmentColor { Green, Orange, Red }
    private record ColorSegment(SegmentColor Color, List<int> Indices);

    public HistoryChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RenderChart();
    }

    internal void SetData(IReadOnlyList<HistoryRecord> records, string label)
    {
        _points = records
            .Where(r => r.Limits.ContainsKey(label))
            .Select(r => (
                Ts: DateTimeOffset.Parse(r.Timestamp, CultureInfo.InvariantCulture),
                Value: r.Limits[label]))
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
        var windowStart = windowEnd.AddDays(-14);
        var windowSpanSeconds = (windowEnd - windowStart).TotalSeconds;

        var processed = ProcessGaps(_points);
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
                double y = PadY + (1.0 - processed[i].Value / 100.0) * (h - 2 * PadY);
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
                    // Immediate drop to 0 after last real point
                    result.Add((raw[i].Ts.AddSeconds(1), 0.0));
                    // Stay at 0 until just before next real point
                    result.Add((raw[i + 1].Ts.AddSeconds(-1), 0.0));
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
        value < 75.0 ? SegmentColor.Green :
        value < 90.0 ? SegmentColor.Orange :
        SegmentColor.Red;

    private static Color GetColor(SegmentColor seg) => seg switch
    {
        SegmentColor.Green => Color.FromRgb(0x4C, 0xAF, 0x50),
        SegmentColor.Orange => Color.FromRgb(0xFF, 0x98, 0x00),
        SegmentColor.Red => Color.FromRgb(0xF4, 0x43, 0x36),
        _ => Color.FromRgb(0x4C, 0xAF, 0x50)
    };
}
