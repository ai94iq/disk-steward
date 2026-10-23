using DiskSteward.Core.Safety;

namespace DiskSteward.Core.Apps;

// What an uninstalled app may have left behind. Exact names only: the recorded install
// folder, and folders or Start Menu entries named exactly after the app (also under its
// publisher). Never a vendor-wide folder, a partial name or a guess.
public sealed class LeftoverFinder(ProtectedPaths rules)
{
    public IReadOnlyList<string> Find(InstalledApp app, LeftoverRoots roots)
    {
        var candidates = new List<string>();
        if (app.InstallLocation is { Length: > 0 } install && LocalPath.IsOnLocalDrive(install)) candidates.Add(install);
        foreach (var root in roots.DataFolders)
        {
            Add(candidates, Under(root, app.Name));
            if (app.Publisher is { Length: > 0 } publisher) Add(candidates, Under(root, publisher, app.Name));
        }
        foreach (var root in roots.ShortcutFolders)
        {
            Add(candidates, Under(root, app.Name));
            Add(candidates, Under(root, app.Name + ".lnk"));
        }

        return candidates
            .Where(p => !LocalPath.CrossesLink(p)) // a link here could lead off the drive; checked before any stat
            .Where(p => Directory.Exists(p) || File.Exists(p))
            .Where(p => !rules.IsProtected(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Add(List<string> candidates, string? path)
    {
        if (path is not null) candidates.Add(path);
    }

    // A candidate built from the app's own name must stay inside the root being searched.
    // DisplayName and Publisher come from the registry, where any user-level code can write, so
    // a value like "C:\Users\me\Documents" or "..\..\Documents" would otherwise point the
    // candidate at an unrelated folder (Path.Combine drops the root when a part is rooted, and
    // keeps "..", which GetFullPath then resolves outward). Null means the name escaped the
    // root and is not offered.
    private static string? Under(string root, params string[] parts)
    {
        var full = Path.GetFullPath(Path.Combine([root, .. parts]));
        var baseDir = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        return full.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
