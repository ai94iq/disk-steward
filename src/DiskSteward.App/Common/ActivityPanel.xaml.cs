using System.Collections;
using System.Windows.Controls;

namespace DiskSteward.App.Common;

public sealed partial class ActivityPanel : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(ActivityPanel), new PropertyMetadata(null));

    public ActivityPanel() => InitializeComponent();

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
}
