using System.Windows.Documents;
using System.Windows.Media;
using MoleWin.App.Resources;

namespace MoleWin.App.Common;

// Outline icon drawn from Resources/Icons.g.cs. Usage: <common:AppIcon Kind="Search" />.
// Takes the surrounding text color. Not mirrored in RTL unless IsDirectional="True"
// (use that for arrows, chevrons, back/forward, send).
public sealed class AppIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(IconKind), typeof(AppIcon),
        new FrameworkPropertyMetadata(default(IconKind), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDirectionalProperty = DependencyProperty.Register(
        nameof(IsDirectional), typeof(bool), typeof(AppIcon), new PropertyMetadata(false, OnDirectionalChanged));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(AppIcon),
        new FrameworkPropertyMetadata(Brushes.Black,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Dictionary<IconKind, Geometry> Cache = [];

    public AppIcon()
    {
        FlowDirection = FlowDirection.LeftToRight;
        SnapsToDevicePixels = true;
    }

    public IconKind Kind
    {
        get => (IconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsDirectional
    {
        get => (bool)GetValue(IsDirectionalProperty);
        set => SetValue(IsDirectionalProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(20, 20);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        drawingContext.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        drawingContext.DrawGeometry(Foreground, null, GeometryFor(Kind));
        drawingContext.Pop();
    }

    private static Geometry GeometryFor(IconKind kind)
    {
        if (!Cache.TryGetValue(kind, out var geometry))
        {
            geometry = Geometry.Parse(IconData.Get(kind));
            geometry.Freeze();
            Cache[kind] = geometry;
        }

        return geometry;
    }

    private static void OnDirectionalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var icon = (AppIcon)d;
        if ((bool)e.NewValue) icon.ClearValue(FlowDirectionProperty);   // inherit, so it mirrors in RTL
        else icon.FlowDirection = FlowDirection.LeftToRight;
    }
}
