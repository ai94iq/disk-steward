using System.Windows.Data;
using MoleWin.App.Resources;
using Wpf.Ui.Controls;

namespace MoleWin.App.Common;

// Lets WPF-UI Icon slots (NavigationViewItem, Button) use AppIcon outlines instead of an icon font.
public sealed class AppIconElement : IconElement
{
    public IconKind Kind { get; set; }

    protected override UIElement InitializeChildren()
    {
        var icon = new AppIcon { Kind = Kind };
        icon.SetBinding(AppIcon.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        return icon;
    }
}
