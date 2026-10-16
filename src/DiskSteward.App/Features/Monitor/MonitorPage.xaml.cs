using System.Windows.Controls;

namespace DiskSteward.App.Features.Monitor;

// A Page using WPF-UI's Page style (set in XAML; implicit styles skip subclasses), which
// gives the theme text color that does not flow through the navigation frame.
// Sampling runs only while the page is on screen.
public sealed partial class MonitorPage : Page
{
    public MonitorPage(MonitorViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await viewModel.EnsureLoadedAsync();
            if (IsLoaded) viewModel.StartLive();
        };
        Unloaded += (_, _) => viewModel.StopLive();
    }
}
