using System.Windows.Controls;

namespace MoleWin.App.Features.Analyze;

// A Page using WPF-UI's Page style (set in XAML; implicit styles skip subclasses), which
// gives the theme text color that does not flow through the navigation frame.
public sealed partial class AnalyzePage : Page
{
    public AnalyzePage(AnalyzeViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await viewModel.EnsureLoadedAsync();
    }
}
