using Wpf.Ui.Controls;

namespace MoleWin.App.Shell;

public sealed partial class MainWindow : FluentWindow
{
    public MainWindow(MainWindowViewModel viewModel, IThemeService theme)
    {
        DataContext = viewModel;
        InitializeComponent();
        this.ApplyCultureDirection();

        // Mica exists only on Windows 11; Windows 10 gets the solid theme background.
        WindowBackdropType = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? WindowBackdropType.Mica
            : WindowBackdropType.None;
        theme.Attach(this);
    }
}
