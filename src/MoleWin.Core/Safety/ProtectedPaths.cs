namespace MoleWin.Core.Safety;

// Decides what may never be removed. Every apply path asks this right before acting, so a
// stale or hand-built list cannot reach a protected folder. When in doubt it says protected.
public sealed class ProtectedPaths(ProtectedRoots roots)
{
    private static readonly string[] VolumeSystemFolders = ["$Recycle.Bin", "System Volume Information"];

    // Files Windows keeps at a drive root to page, hibernate, swap and boot. Removing them
    // breaks the system or frees nothing until reboot; never offered.
    private static readonly string[] VolumeSystemFiles = ["pagefile.sys", "hiberfil.sys", "swapfile.sys", "bootmgr", "BOOTNXT"];

    // Protected with everything inside, each with the reason the screen gives.
    private readonly (string Folder, ProtectionReason Reason)[] _withContents =
    [
        (Normalize(roots.Windows), ProtectionReason.Windows),
        (Normalize(roots.ProgramData), ProtectionReason.ProgramData),
        (Normalize(roots.AppOwnData), ProtectionReason.AppOwnData),
        .. roots.ProgramFiles.Select(p => (Normalize(p), ProtectionReason.Programs)),
    ];

    private readonly string[] _exactOnly =
    [
        Normalize(Path.GetDirectoryName(roots.UserProfile) ?? roots.UserProfile),
        Normalize(roots.UserProfile),
        Normalize(Path.Combine(roots.UserProfile, "AppData")),
        .. roots.KnownFolders.Select(Normalize),
    ];

    public bool IsProtected(string path) => ReasonFor(path) != ProtectionReason.None;

    // None when the path may be removed; otherwise the first rule that protects it.
    public ProtectionReason ReasonFor(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return ProtectionReason.NotAFullPath;

        var full = Normalize(path);
        var root = Normalize(Path.GetPathRoot(full) ?? "");
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return ProtectionReason.DriveRoot;

        var segments = full[root.Length..].TrimStart(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
        if (VolumeSystemFolders.Contains(segments[0], StringComparer.OrdinalIgnoreCase)) return ProtectionReason.VolumeSystem;
        if (segments.Length == 1
            && (VolumeSystemFiles.Contains(segments[0], StringComparer.OrdinalIgnoreCase)
                || segments[0].StartsWith("DumpStack.log", StringComparison.OrdinalIgnoreCase)))
            return ProtectionReason.VolumeSystem;

        if (_exactOnly.Any(p => full.Equals(p, StringComparison.OrdinalIgnoreCase))) return ProtectionReason.PersonalFolder;
        return _withContents.FirstOrDefault(p => p.Folder.Length > 0 && IsSameOrInside(full, p.Folder)).Reason;
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
