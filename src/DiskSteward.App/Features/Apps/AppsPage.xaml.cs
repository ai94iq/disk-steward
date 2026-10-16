using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DiskSteward.App.Features.Apps;

// A Page using WPF-UI's Page style (set in XAML; implicit styles skip subclasses), which
// gives the theme text color that does not flow through the navigation frame.
public sealed partial class AppsPage : Page
{
    public AppsPage(AppsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await viewModel.EnsureLoadedAsync();
        // The page takes keyboard focus when opened so its shortcuts (F5, Ctrl+F) work at once.
        Focusable = true;
        FocusVisualStyle = null;
        Loaded += (_, _) => Focus();
        InputBindings.Add(new KeyBinding(new RelayCommand(() => FindSearchBox(this)?.Focus()), Key.F, ModifierKeys.Control));
    }

    // The search box lives in the header's Actions, where XAML names are not allowed.
    private static Wpf.Ui.Controls.TextBox? FindSearchBox(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Wpf.Ui.Controls.TextBox box) return box;
            if (FindSearchBox(child) is { } found) return found;
        }
        return null;
    }
}
