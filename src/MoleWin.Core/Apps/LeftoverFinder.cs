using MoleWin.Core.Safety;

namespace MoleWin.Core.Apps;

// What an uninstalled app may have left behind. Exact names only: the recorded install
// folder, and folders or Start Menu entries named exactly after the app (also under its
// publisher). Never a vendor-wide folder, a partial name or a guess.
public sealed class LeftoverFinder(ProtectedPaths rules)
{
    public IReadOnlyList<string> Find(InstalledApp app, LeftoverRoots roots)
    {
        var candidates = new List<string>();
        if (app.InstallLocation is { Length: > 0 } install && Path.IsPathFullyQualified(install)) candidates.Add(install);
        foreach (var root in roots.DataFolders)
        {
            candidates.Add(Path.Combine(root, app.Name));
            if (app.Publisher is { Length: > 0 } publisher) candidates.Add(Path.Combine(root, publisher, app.Name));
        }
        foreach (var root in roots.ShortcutFolders)
        {
            candidates.Add(Path.Combine(root, app.Name));
            candidates.Add(Path.Combine(root, app.Name + ".lnk"));
        }

        return candidates
            .Where(p => Directory.Exists(p) || File.Exists(p))
            .Where(p => !rules.IsProtected(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
