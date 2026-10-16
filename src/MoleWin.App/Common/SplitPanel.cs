using System.Windows.Controls;

namespace MoleWin.App.Common;

// Main content with a side panel: on the right while the panel is at least Breakpoint wide,
// below the main content when it is narrower, so neither gets squeezed to nothing.
// Children: [0] main, [1] side. A collapsed side gives the main content all the room.
public sealed class SplitPanel : Panel
{
    public static readonly DependencyProperty BreakpointProperty = DependencyProperty.Register(
        nameof(Breakpoint), typeof(double), typeof(SplitPanel), new FrameworkPropertyMetadata(700.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SideWidthProperty = DependencyProperty.Register(
        nameof(SideWidth), typeof(double), typeof(SplitPanel), new FrameworkPropertyMetadata(280.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SideHeightProperty = DependencyProperty.Register(
        nameof(SideHeight), typeof(double), typeof(SplitPanel), new FrameworkPropertyMetadata(180.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(SplitPanel), new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Breakpoint { get => (double)GetValue(BreakpointProperty); set => SetValue(BreakpointProperty, value); }
    public double SideWidth { get => (double)GetValue(SideWidthProperty); set => SetValue(SideWidthProperty, value); }
    public double SideHeight { get => (double)GetValue(SideHeightProperty); set => SetValue(SideHeightProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (main, side) = Layout(availableSize);
        if (Children.Count > 0) Children[0].Measure(main.Size);
        if (Children.Count > 1) Children[1].Measure(side.Size);
        return new Size(
            double.IsInfinity(availableSize.Width) ? Children.Cast<UIElement>().Sum(c => c.DesiredSize.Width) : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? Children.Cast<UIElement>().Sum(c => c.DesiredSize.Height) : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (main, side) = Layout(finalSize);
        if (Children.Count > 0) Children[0].Arrange(main);
        if (Children.Count > 1) Children[1].Arrange(side);
        return finalSize;
    }

    private (Rect Main, Rect Side) Layout(Size size)
    {
        var width = double.IsInfinity(size.Width) ? 0 : size.Width;
        var height = double.IsInfinity(size.Height) ? 0 : size.Height;
        if (Children.Count < 2 || Children[1].Visibility == Visibility.Collapsed)
            return (new Rect(0, 0, width, height), default);
        if (width >= Breakpoint)
        {
            var mainWidth = Math.Max(0, width - SideWidth - Gap);
            return (new Rect(0, 0, mainWidth, height), new Rect(mainWidth + Gap, 0, SideWidth, height));
        }
        // Stacked, the side never takes more than a third of a short page.
        var sideHeight = Math.Min(SideHeight, height * 0.35);
        var mainHeight = Math.Max(0, height - sideHeight - Gap);
        return (new Rect(0, 0, width, mainHeight), new Rect(0, mainHeight + Gap, width, sideHeight));
    }
}
