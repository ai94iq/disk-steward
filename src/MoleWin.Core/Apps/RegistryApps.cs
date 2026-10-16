using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MoleWin.Core.Apps;

// The three Uninstall keys Windows' own "Installed apps" list reads: machine 64-bit, machine
// 32-bit and current user. Read-only.
[SupportedOSPlatform("windows")]
public sealed class RegistryApps : IInstalledApps
{
    private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Uninstall32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly (RegistryKey Hive, string Path)[] Roots =
    [
        (Registry.LocalMachine, Uninstall),
        (Registry.LocalMachine, Uninstall32),
        (Registry.CurrentUser, Uninstall),
    ];

    public IReadOnlyList<InstalledApp> List()
    {
        var apps = new List<InstalledApp>();
        foreach (var (hive, path) in Roots)
        {
            using var root = hive.OpenSubKey(path);
            if (root is null) continue;
            foreach (var name in root.GetSubKeyNames())
            {
                using var entry = root.OpenSubKey(name);
                if (entry is null) continue;
                var values = entry.GetValueNames().ToDictionary(v => v, v => entry.GetValue(v), StringComparer.OrdinalIgnoreCase);
                if (InstalledAppParser.FromValues($@"{hive.Name}\{path}\{name}", values) is { } app) apps.Add(app);
            }
        }
        // The same app can be registered twice (per-user and machine); keep one.
        return apps.GroupBy(a => (a.Name, a.Version)).Select(g => g.First()).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public bool IsInstalled(InstalledApp app)
    {
        var (hive, path) = Split(app.Key);
        using var key = hive?.OpenSubKey(path);
        return key is not null;
    }

    private static (RegistryKey? Hive, string Path) Split(string key)
    {
        var slash = key.IndexOf('\\', StringComparison.Ordinal);
        if (slash < 0) return (null, "");
        var hive = key[..slash] switch
        {
            "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" => Registry.CurrentUser,
            _ => null,
        };
        return (hive, key[(slash + 1)..]);
    }
}
