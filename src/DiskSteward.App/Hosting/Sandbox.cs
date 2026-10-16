#if DEBUG
using DiskSteward.App.Services;
using DiskSteward.Core.Analyze;
using DiskSteward.Core.Apps;
using DiskSteward.Core.Clean;
using DiskSteward.Core.Safety;

namespace DiskSteward.App.Hosting;

// Debug builds only: DISKSTEWARD_SANDBOX=<folder> points every page at fake data under that folder
// so actions can be tried and timed without touching the real machine.
//   <folder>\clean\<rule id>   fake caches for Clean; each folder stands in for that real rule
//   <folder>\drive             the one "drive" Analyze shows
//   <folder>\apps, \appdata    fake installs and their leftovers for Apps
//   <folder>\RecycleBin        where "recycled" items go, unless DISKSTEWARD_SANDBOX_BIN=windows
//                              sends them (sandbox files only) to the real Recycle Bin
// DISKSTEWARD_PAGE=Clean|Apps|Analyze|Monitor picks the start page. The folder must already hold
// the fake data (tools/sandbox.ps1 makes it).
public static class Sandbox
{
    public static string? Root { get; } = Environment.GetEnvironmentVariable("DISKSTEWARD_SANDBOX") is { Length: > 0 } root
        && Directory.Exists(root) ? Path.GetFullPath(root) : null;

    public static Type? StartPage => Environment.GetEnvironmentVariable("DISKSTEWARD_PAGE") is { Length: > 0 } page
        ? Type.GetType($"DiskSteward.App.Features.{page}.{page}Page") : null;

    public static void Apply(IServiceCollection services)
    {
        if (Root is not { } root) return;

        IRecycleBin bin = Environment.GetEnvironmentVariable("DISKSTEWARD_SANDBOX_BIN") == "windows"
            ? new WindowsRecycleBin()
            : new FolderBin(Path.Combine(root, "RecycleBin"));
        services.AddSingleton(bin);
        // The real rules, each pointed at its sandbox folder when one exists, with no age or
        // name filter so every fake file counts.
        services.AddSingleton<IReadOnlyList<CleanTarget>>(
        [
            .. CleanRules.Defaults
                .Select(rule => rule with { Path = Path.Combine(root, "clean", rule.Id), Mode = CleanMode.Contents, Filter = "*", OlderThanDays = 0 })
                .Where(rule => Directory.Exists(rule.Path)),
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
        public void MoveToRecycleBin(IReadOnlyList<string> paths)
        {
            foreach (var path in paths)
            {
                var target = Path.Combine(bin, Guid.NewGuid().ToString("N"), Path.GetFileName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (Directory.Exists(path)) Directory.Move(path, target);
                else File.Move(path, target);
            }
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
