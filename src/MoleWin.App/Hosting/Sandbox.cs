#if DEBUG
using MoleWin.App.Services;
using MoleWin.Core.Analyze;
using MoleWin.Core.Apps;
using MoleWin.Core.Clean;
using MoleWin.Core.Safety;

namespace MoleWin.App.Hosting;

// Debug builds only: MOLEWIN_SANDBOX=<folder> points every page at fake data under that folder
// so actions can be tried and timed without touching the real machine.
//   <folder>\clean\<rule id>   fake caches for Clean, one per category
//   <folder>\drive             the one "drive" Analyze shows
//   <folder>\apps, \appdata    fake installs and their leftovers for Apps
//   <folder>\RecycleBin        where "recycled" items go; the Windows Recycle Bin is never used
// MOLEWIN_PAGE=Clean|Apps|Analyze|Monitor picks the start page. The folder must already hold
// the fake data (tools/sandbox.ps1 makes it).
public static class Sandbox
{
    public static string? Root { get; } = Environment.GetEnvironmentVariable("MOLEWIN_SANDBOX") is { Length: > 0 } root
        && Directory.Exists(root) ? Path.GetFullPath(root) : null;

    public static Type? StartPage => Environment.GetEnvironmentVariable("MOLEWIN_PAGE") is { Length: > 0 } page
        ? Type.GetType($"MoleWin.App.Features.{page}.{page}Page") : null;

    public static void Apply(IServiceCollection services)
    {
        if (Root is not { } root) return;

        services.AddSingleton<IRecycleBin>(new FolderBin(Path.Combine(root, "RecycleBin")));
        services.AddSingleton<IReadOnlyList<CleanTarget>>(
        [
            new("temp", CleanCategory.System, Path.Combine(root, "clean", "temp")),
            new("chrome-cache", CleanCategory.Browsers, Path.Combine(root, "clean", "chrome-cache")),
            new("spotify-data", CleanCategory.Apps, Path.Combine(root, "clean", "spotify-data")),
            new("nvidia-dx", CleanCategory.Gpu, Path.Combine(root, "clean", "nvidia-dx")),
            new("npm-cache", CleanCategory.Developer, Path.Combine(root, "clean", "npm-cache")),
            new("cursor-cache", CleanCategory.AiTools, Path.Combine(root, "clean", "cursor-cache")),
        ]);
        services.AddSingleton<IFolderScanner>(new OneDriveScanner(Path.Combine(root, "drive")));
        var apps = new FakeApps(root);
        services.AddSingleton<IInstalledApps>(apps);
        services.AddSingleton<IUninstallerRunner>(apps);
        services.AddSingleton(new LeftoverRoots([Path.Combine(root, "appdata")], [Path.Combine(root, "shortcuts")]));
    }

    // "Recycles" by moving into a folder inside the sandbox.
    private sealed class FolderBin(string bin) : IRecycleBin
    {
        public void MoveToRecycleBin(string path)
        {
            var target = Path.Combine(bin, Guid.NewGuid().ToString("N"), Path.GetFileName(path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (Directory.Exists(path)) Directory.Move(path, target);
            else File.Move(path, target);
        }
    }

    // The real scanner, with the sandbox folder as the only drive (10 GB, as if).
    private sealed class OneDriveScanner(string drive) : IFolderScanner
    {
        private const long Capacity = 10L << 30;
        private readonly FolderScanner _real = new();

        public IReadOnlyList<FolderEntry> ListRoots() =>
            [new FolderEntry(drive, drive, true, _real.Measure(drive, new ScanProgress(), CancellationToken.None), Capacity)];

        public IReadOnlyList<FolderEntry> ListChildren(string folder) => _real.ListChildren(folder);

        public long Measure(string folder, ScanProgress progress, CancellationToken ct) => _real.Measure(folder, progress, ct);
    }

    // Apps installed under <root>\apps. "Uninstalling" waits UninstallDelay, as a vendor
    // uninstaller would, then the app is gone and its <root>\appdata folder stays behind.
    private sealed class FakeApps(string root) : IInstalledApps, IUninstallerRunner
    {
        private static readonly TimeSpan UninstallDelay = TimeSpan.FromSeconds(3);
        private readonly HashSet<string> _removed = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<InstalledApp> List() =>
        [
            .. Directory.EnumerateDirectories(Path.Combine(root, "apps"))
                .Where(dir => !_removed.Contains(Path.GetFileName(dir)))
                .Select(dir => new InstalledApp(Path.GetFileName(dir), Path.GetFileName(dir), "Sandbox", "1.0", 1L << 20,
                    new DateTime(2026, 1, 1), "sandbox", dir)),
        ];

        public bool IsInstalled(InstalledApp app) => !_removed.Contains(app.Key);

        public async Task RunAsync(InstalledApp app, CancellationToken ct)
        {
            await Task.Delay(UninstallDelay, ct);
            _removed.Add(app.Key);
        }
    }
}
#endif
