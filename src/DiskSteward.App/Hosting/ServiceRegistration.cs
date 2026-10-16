using DiskSteward.App.Features.Analyze;
using DiskSteward.App.Features.Apps;
using DiskSteward.Core.Apps;
using DiskSteward.App.Features.Clean;
using DiskSteward.Core.Clean;
using DiskSteward.App.Features.Monitor;
using DiskSteward.App.Shell;
using DiskSteward.Core.Analyze;
using DiskSteward.Core.Monitor;
using DiskSteward.Core.Safety;

namespace DiskSteward.App.Hosting;

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

        // Engine (DiskSteward.Core). Removal only ever goes through SafeRemover.
        services.AddSingleton(new ProtectedPaths(ProtectedRoots.ForCurrentUser(AppPaths.Root)));
        services.AddSingleton<IRecycleBin, WindowsRecycleBin>();
        services.AddSingleton(sp => new SafeRemover(
            sp.GetRequiredService<ProtectedPaths>(), sp.GetRequiredService<IRecycleBin>(), IsOnFixedDrive));
        services.AddSingleton<IFolderScanner, FolderScanner>();
        services.AddTransient<IMonitorService, WindowsMonitor>();
        services.AddSingleton(sp => new CleanScanner(
            sp.GetRequiredService<IFolderScanner>(), sp.GetRequiredService<ProtectedPaths>(), CleanRules.Whitelist));
        services.AddSingleton(CleanRules.Defaults);
        services.AddSingleton<IInstalledApps, RegistryApps>();
        services.AddSingleton<IUninstallerRunner, UninstallerRunner>();
        services.AddSingleton(sp => new LeftoverFinder(sp.GetRequiredService<ProtectedPaths>()));
        services.AddSingleton(LeftoverRoots.ForCurrentUser());

        // Shell: Singleton. Pages: Transient. The NavigationView resolves pages from this provider.
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<AnalyzePage>();
        // Singleton so a long scan and its activity survive switching pages.
        services.AddSingleton<AnalyzeViewModel>();
        services.AddTransient<AppsPage>();
        // Singleton so an uninstall in progress and its leftovers survive switching pages.
        services.AddSingleton<AppsViewModel>();
        services.AddTransient<CleanPage>();
        // Singleton so the scan result survives switching pages.
        services.AddSingleton<CleanViewModel>();
        services.AddTransient<MonitorPage>();
        // Singleton so the keep-awake choice it shows matches what is in effect.
        services.AddSingleton<MonitorViewModel>();
        services.AddSingleton<IKeepAwake, KeepAwake>();
        return services;
    }

    // Only fixed local drives have a Recycle Bin; anywhere else Windows deletes for good, so
    // anything that is not plainly a fixed drive (UNC shares, which make DriveInfo throw,
    // removable and network drives) is treated as not recyclable.
    private static bool IsOnFixedDrive(string path)
    {
        try
        {
            return Path.GetPathRoot(path) is { Length: > 0 } root
                && !root.StartsWith(@"\\", StringComparison.Ordinal)
                && new DriveInfo(root).DriveType == DriveType.Fixed;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
