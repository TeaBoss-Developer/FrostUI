using System.Windows;
using System.Windows.Media;

namespace FrostUI.Controls;

/// <summary>A compact smooth sparkline that renders a line and an optional gradient area.</summary>
public class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(DoubleCollection),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(new DoubleCollection(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(1.8d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowLastDotProperty = DependencyProperty.Register(
        nameof(ShowLastDot),
        typeof(bool),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding),
        typeof(Thickness),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(new Thickness(2), FrameworkPropertyMetadataOptions.AffectsRender));

    public DoubleCollection Values
    {
        get => (DoubleCollection)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool ShowLastDot
    {
        get => (bool)GetValue(ShowLastDotProperty);
        set => SetValue(ShowLastDotProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var values = Values;
        if (values is null || values.Count < 2 || ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        var padding = Padding;
        var width = ActualWidth - padding.Left - padding.Right;
        var height = ActualHeight - padding.Top - padding.Bottom;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 0.0001)
        {
            max = min + 1;
        }

        var points = new Point[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var x = padding.Left + (width * i / (values.Count - 1d));
            var normalized = (values[i] - min) / (max - min);
            var y = padding.Top + (height * (1d - normalized));
            points[i] = new Point(x, y);
        }

        var line = BuildGeometry(points, closeToBottom: false);
        var area = BuildGeometry(points, closeToBottom: true);

        if (Fill is not null)
        {
            drawingContext.DrawGeometry(Fill, null, area);
        }

        var pen = new Pen(Stroke, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        drawingContext.DrawGeometry(null, pen, line);

        if (ShowLastDot)
        {
            var dot = points[^1];
            drawingContext.DrawEllipse(Stroke, null, dot, StrokeThickness + 1.1, StrokeThickness + 1.1);
            drawingContext.DrawEllipse(Brushes.White, null, dot, StrokeThickness * 0.5, StrokeThickness * 0.5);
        }
    }

    private Geometry BuildGeometry(IReadOnlyList<Point> points, bool closeToBottom)
    {
        var figure = new PathFigure
        {
            StartPoint = closeToBottom
                ? new Point(points[0].X, ActualHeight - Padding.Bottom)
                : points[0],
            IsClosed = closeToBottom,
            IsFilled = closeToBottom,
        };

        if (closeToBottom)
        {
            figure.Segments.Add(new LineSegment(points[0], true));
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < points.Count ? points[i + 2] : p2;

            var c1 = new Point(
                p1.X + ((p2.X - p0.X) / 6d),
                p1.Y + ((p2.Y - p0.Y) / 6d));
            var c2 = new Point(
                p2.X - ((p3.X - p1.X) / 6d),
                p2.Y - ((p3.Y - p1.Y) / 6d));

            figure.Segments.Add(new BezierSegment(c1, c2, p2, true));
        }

        if (closeToBottom)
        {
            figure.Segments.Add(new LineSegment(
                new Point(points[^1].X, ActualHeight - Padding.Bottom),
                true));
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }
}
