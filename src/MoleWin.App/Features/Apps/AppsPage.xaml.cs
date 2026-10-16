using System.Windows.Controls;

namespace MoleWin.App.Features.Apps;

// A Page using WPF-UI's Page style (set in XAML; implicit styles skip subclasses), which
// gives the theme text color that does not flow through the navigation frame.
public sealed partial class AppsPage : Page
{
    public AppsPage(AppsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await viewModel.EnsureLoadedAsync();
    }
}
