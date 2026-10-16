using System.Windows.Media;

namespace MoleWin.App.Common;

// A rolling line with a soft area under it, oldest sample on the left. The x axis is fixed to
// Capacity samples so the line grows in from the right instead of stretching.
// Maximum: the value at the top edge; NaN scales to the largest sample (rates with no ceiling),
// but never below Floor, so an idle link reads as flat instead of magnified noise.
public sealed class HistoryChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(HistoryChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(HistoryChart),
        new FrameworkPropertyMetadata(30, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(HistoryChart),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FloorProperty = DependencyProperty.Register(
        nameof(Floor), typeof(double), typeof(HistoryChart),
        new FrameworkPropertyMetadata(0.001, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(HistoryChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Floor
    {
        get => (double)GetValue(FloorProperty);
        set => SetValue(FloorProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    // Optional recessive grid at 25, 50 and 75 percent plus the baseline; null draws none.
    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(HistoryChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? GridBrush
    {
        get => (Brush?)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        if (GridBrush is { } grid)
        {
            var pen = new Pen(grid, 1);
            foreach (var fraction in (double[])[0.25, 0.5, 0.75, 1])
            {
                var y = Math.Round(ActualHeight * fraction) - 0.5;
                dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
            }
        }

        var values = Values;
        if (values is not { Count: > 1 } || Stroke is null) return;

        var top = double.IsNaN(Maximum) ? Math.Max(values.Max(), Floor) : Maximum;
        var step = ActualWidth / Math.Max(Capacity - 1, 1);
        var left = ActualWidth - step * (values.Count - 1);
        Point At(int i) => new(left + step * i, ActualHeight * (1 - Math.Clamp(values[i] / top, 0, 1)));

        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(At(0), false, false);
            for (var i = 1; i < values.Count; i++) ctx.LineTo(At(i), true, true);
        }
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(left, ActualHeight), true, true);
            for (var i = 0; i < values.Count; i++) ctx.LineTo(At(i), false, true);
            ctx.LineTo(new Point(ActualWidth, ActualHeight), false, true);
        }
        line.Freeze();
        area.Freeze();

        // The area is the stroke at low opacity: one color, no gradient.
        dc.PushOpacity(0.18);
        dc.DrawGeometry(Stroke, null, area);
        dc.Pop();
        dc.DrawGeometry(null, new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round }, line);

        // The newest sample, so "now" reads at a glance.
        dc.DrawEllipse(Stroke, null, At(values.Count - 1), 4, 4);
    }
}
