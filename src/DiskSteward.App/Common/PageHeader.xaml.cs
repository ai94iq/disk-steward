using System.Windows.Controls;

namespace DiskSteward.App.Common;

public sealed partial class PageHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(null));

    public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
        nameof(Summary), typeof(string), typeof(PageHeader), new PropertyMetadata(null));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    // Room the title keeps before the actions move under it.
    private const double TitleMinWidth = 240;

    public PageHeader()
    {
        InitializeComponent();
        SizeChanged += (_, _) => PlaceActions();
    }

    // Beside the title while both fit, otherwise on their own row under it.
    private void PlaceActions()
    {
        ActionsHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var below = ActualWidth < TitleMinWidth + ActionsHost.DesiredSize.Width;
        Grid.SetRow(ActionsHost, below ? 1 : 0);
        Grid.SetColumn(ActionsHost, below ? 0 : 1);
        Grid.SetColumnSpan(ActionsHost, below ? 2 : 1);
        ActionsHost.HorizontalAlignment = below ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        ActionsHost.Margin = below ? new Thickness(0, 12, 0, 0) : default;
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Summary
    {
        get => (string?)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
