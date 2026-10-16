using System.Windows.Controls;
using System.Windows.Input;

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
        // The page takes keyboard focus when opened so its shortcuts (F5, Del) work at once.
        Focusable = true;
        FocusVisualStyle = null;
        Loaded += (_, _) => Focus();
    }

    // Space ticks or unticks the focused row, like the tick box itself; locked rows ignore it.
    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || ((TreeView)sender).SelectedItem is not FolderNode { CanSelect: true } node) return;
        node.IsSelected = !node.IsSelected;
        e.Handled = true;
    }
}
