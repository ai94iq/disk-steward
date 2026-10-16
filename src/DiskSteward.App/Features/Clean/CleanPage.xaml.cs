using System.Windows.Controls;

namespace DiskSteward.App.Features.Clean;

// A Page using WPF-UI's Page style (set in XAML; implicit styles skip subclasses), which
// gives the theme text color that does not flow through the navigation frame.
// The read-only scan starts when the page first opens.
public sealed partial class CleanPage : Page
{
    public CleanPage(CleanViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await viewModel.EnsureLoadedAsync();
        // The page takes keyboard focus when opened so its shortcuts (F5, Del) work at once.
        Focusable = true;
        FocusVisualStyle = null;
        Loaded += (_, _) => Focus();
    }
}
