using MoleWin.App.Features.Monitor;
using Wpf.Ui.Controls;

namespace MoleWin.App.Shell;

public sealed partial class MainWindow : FluentWindow
{
    private const double CompactNavBelow = 1000;

    public MainWindow(MainWindowViewModel viewModel, IThemeService theme, IServiceProvider services)
    {
        DataContext = viewModel;
        InitializeComponent();
        RootNavigation.SetServiceProvider(services);
        Loaded += (_, _) => RootNavigation.Navigate(typeof(MonitorPage));
        this.ApplyCultureDirection();

        // Below this width the nav pane shows icons only, so pages keep their room.
        SizeChanged += (_, e) => RootNavigation.IsPaneOpen = e.NewSize.Width >= CompactNavBelow;

        // Mica exists only on Windows 11; Windows 10 gets the solid theme background.
        WindowBackdropType = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? WindowBackdropType.Mica
            : WindowBackdropType.None;
        theme.Attach(this);
    }
}
