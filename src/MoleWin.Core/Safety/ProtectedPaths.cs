namespace MoleWin.Core.Safety;

// Decides what may never be removed. Every apply path asks this right before acting, so a
// stale or hand-built list cannot reach a protected folder. When in doubt it says protected.
public sealed class ProtectedPaths(ProtectedRoots roots)
{
    private static readonly string[] VolumeSystemFolders = ["$Recycle.Bin", "System Volume Information"];

    private readonly string[] _withContents =
        [Normalize(roots.Windows), Normalize(roots.ProgramData), Normalize(roots.AppOwnData), .. roots.ProgramFiles.Select(Normalize)];

    private readonly string[] _exactOnly =
    [
        Normalize(Path.GetDirectoryName(roots.UserProfile) ?? roots.UserProfile),
        Normalize(roots.UserProfile),
        Normalize(Path.Combine(roots.UserProfile, "AppData")),
        .. roots.KnownFolders.Select(Normalize),
    ];

    public bool IsProtected(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return true;

        var full = Normalize(path);
        var root = Normalize(Path.GetPathRoot(full) ?? "");
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return true;

        var firstSegment = full[root.Length..].TrimStart(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar)[0];
        if (VolumeSystemFolders.Contains(firstSegment, StringComparer.OrdinalIgnoreCase)) return true;

        return _exactOnly.Any(p => full.Equals(p, StringComparison.OrdinalIgnoreCase))
            || _withContents.Any(p => p.Length > 0 && IsSameOrInside(full, p));
    }

    private static bool IsSameOrInside(string path, string folder) =>
        path.Equals(folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    // Resolves ".." and trims trailing separators so comparisons see one spelling per folder.
    // A drive root keeps its separator: "C:" alone would mean "the current folder on C".
    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return "";
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "";
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length > root.TrimEnd(Path.DirectorySeparatorChar).Length ? trimmed : root;
    }
}
