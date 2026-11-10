using System.Windows;
using System.Windows.Media;

namespace FrostUI.Controls;

/// <summary>A lightweight circular progress indicator drawn directly by WPF.</summary>
public class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(8d, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceThickness));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register(
        nameof(ProgressBrush),
        typeof(Brush),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowBrushProperty = DependencyProperty.Register(
        nameof(GlowBrush),
        typeof(Brush),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartAngleProperty = DependencyProperty.Register(
        nameof(StartAngle),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(-90d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Current value, in the range defined by <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    /// <summary>Optional wide, soft stroke drawn under the progress arc.</summary>
    public Brush? GlowBrush
    {
        get => (Brush?)GetValue(GlowBrushProperty);
        set => SetValue(GlowBrushProperty, value);
    }

    public double StartAngle
    {
        get => (double)GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var thickness = StrokeThickness;
        var radius = (Math.Min(ActualWidth, ActualHeight) - thickness) / 2d;
        if (radius <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2d, ActualHeight / 2d);
        var trackPen = new Pen(TrackBrush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        trackPen.Freeze();
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var range = Maximum - Minimum;
        if (range <= 0)
        {
            return;
        }

        var fraction = Math.Clamp((Value - Minimum) / range, 0d, 1d);
        if (fraction <= 0d)
        {
            return;
        }

        if (fraction >= 0.9999)
        {
            var fullPen = new Pen(ProgressBrush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            fullPen.Freeze();
            if (GlowBrush is not null)
            {
                var glowPen = new Pen(GlowBrush, thickness + 8);
                glowPen.Freeze();
                drawingContext.DrawEllipse(null, glowPen, center, radius, radius);
            }

            drawingContext.DrawEllipse(null, fullPen, center, radius, radius);
            return;
        }

        var startAngle = StartAngle;
        var endAngle = startAngle + (fraction * 360d);
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, endAngle);

        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
            IsFilled = false,
        };
        figure.Segments.Add(new ArcSegment(
            end,
            new Size(radius, radius),
            0,
            fraction > 0.5,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();

        if (GlowBrush is not null)
        {
            var glowPen = new Pen(GlowBrush, thickness + 8)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            glowPen.Freeze();
            drawingContext.DrawGeometry(null, glowPen, geometry);
        }

        var progressPen = new Pen(ProgressBrush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        progressPen.Freeze();
        drawingContext.DrawGeometry(null, progressPen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angle)
    {
        var radians = angle * Math.PI / 180d;
        return new Point(
            center.X + (radius * Math.Cos(radians)),
            center.Y + (radius * Math.Sin(radians)));
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProgressRing ring)
        {
            ring.CoerceValue(ValueProperty);
            ring.InvalidateVisual();
        }
    }

    private static object CoerceValue(DependencyObject d, object value)
    {
        var ring = (ProgressRing)d;
        var numeric = (double)value;
        if (numeric < ring.Minimum)
        {
            return ring.Minimum;
        }

        return numeric > ring.Maximum ? ring.Maximum : numeric;
    }

    private static object CoerceThickness(DependencyObject d, object value) =>
        Math.Max(1d, (double)value);
}
