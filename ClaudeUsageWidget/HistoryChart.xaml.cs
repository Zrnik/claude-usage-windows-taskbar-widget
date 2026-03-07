using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClaudeUsageWidgetProvider;

public partial class HistoryChart : UserControl
{
    private const int TargetLength = 336; // 14d × 24h
    private const double PadX = 2.0;
    private const double PadY = 2.0;

    private IReadOnlyList<double>? _values;

    private enum SegmentColor { Green, Orange, Red }
    private record ColorSegment(SegmentColor Color, List<int> Indices);

    public HistoryChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RenderChart();
    }

    public void SetData(IReadOnlyList<double> values, string label)
    {
        _values = values;
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
        if (_values == null || _values.Count == 0)
            return;

        double w = ChartCanvas.ActualWidth;
        double h = ChartCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        var padded = PadToLength(_values, TargetLength);
        var segments = BuildColorSegments(padded);

        foreach (var seg in segments)
        {
            if (seg.Indices.Count < 1)
                continue;

            var color = GetColor(seg.Color);
            var points = new PointCollection();

            foreach (var i in seg.Indices)
            {
                double x = PadX + i * (w - 2 * PadX) / Math.Max(padded.Length - 1, 1);
                double y = PadY + (1.0 - padded[i] / 100.0) * (h - 2 * PadY);
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

    private static double[] PadToLength(IReadOnlyList<double> values, int length)
    {
        var result = new double[length];
        int offset = length - values.Count;
        if (offset < 0) offset = 0;

        for (int i = 0; i < values.Count && (offset + i) < length; i++)
            result[offset + i] = values[i];

        return result;
    }

    private static List<ColorSegment> BuildColorSegments(double[] values)
    {
        var segments = new List<ColorSegment>();
        if (values.Length == 0)
            return segments;

        var currentColor = Classify(values[0]);
        var currentIndices = new List<int> { 0 };

        for (int i = 1; i < values.Length; i++)
        {
            var c = Classify(values[i]);
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
