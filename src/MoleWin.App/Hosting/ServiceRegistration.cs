using MoleWin.App.Features.Analyze;
using MoleWin.App.Features.Clean;
using MoleWin.Core.Clean;
using MoleWin.App.Features.Monitor;
using MoleWin.App.Shell;
using MoleWin.Core.Analyze;
using MoleWin.Core.Monitor;
using MoleWin.Core.Safety;

namespace MoleWin.App.Hosting;

public static class ServiceRegistration
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton(settings);
        services.AddMemoryCache(o => o.SizeLimit = 2_000);
        services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));

        // Data: factory, initializer and repositories are Singletons.
        services.AddSingleton(new DataOptions(AppPaths.Database, AppPaths.Backups));
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();

        // App services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDateFormatter, DateFormatter>();
        services.AddSingleton<IDataChangeNotifier, DataChangeNotifier>();

        // Engine (MoleWin.Core). Removal only ever goes through SafeRemover.
        services.AddSingleton(new ProtectedPaths(ProtectedRoots.ForCurrentUser(AppPaths.Root)));
        services.AddSingleton<IRecycleBin, WindowsRecycleBin>();
        services.AddSingleton(sp => new SafeRemover(
            sp.GetRequiredService<ProtectedPaths>(), sp.GetRequiredService<IRecycleBin>(), IsOnFixedDrive));
        services.AddSingleton<IFolderScanner, FolderScanner>();
        services.AddTransient<IMonitorService, WindowsMonitor>();
        services.AddSingleton(sp => new CleanScanner(
            sp.GetRequiredService<IFolderScanner>(), sp.GetRequiredService<ProtectedPaths>(), CleanRules.Whitelist));
        services.AddSingleton(CleanRules.Defaults);

        // Shell: Singleton. Pages: Transient. The NavigationView resolves pages from this provider.
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<AnalyzePage>();
        // Singleton so a long scan and its activity survive switching pages.
        services.AddSingleton<AnalyzeViewModel>();
        services.AddTransient<CleanPage>();
        // Singleton so the scan result survives switching pages.
        services.AddSingleton<CleanViewModel>();
        services.AddTransient<MonitorPage>();
        // Singleton so the keep-awake choice it shows matches what is in effect.
        services.AddSingleton<MonitorViewModel>();
        services.AddSingleton<IKeepAwake, KeepAwake>();
        return services;
    }

    // Only fixed local drives have a Recycle Bin; anywhere else Windows deletes for good.
    private static bool IsOnFixedDrive(string path) =>
        Path.GetPathRoot(path) is { Length: > 0 } root && new DriveInfo(root).DriveType == DriveType.Fixed;
}
