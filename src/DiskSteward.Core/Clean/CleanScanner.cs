using System.Text.RegularExpressions;
using DiskSteward.Core.Analyze;
using DiskSteward.Core.Safety;

namespace DiskSteward.Core.Clean;

// Finds what each target holds. Read-only: removal happens later, only for items the user
// ticks, through SafeRemover. Whitelisted and protected paths never become items.
public sealed class CleanScanner(IFolderScanner folders, ProtectedPaths rules, IReadOnlyList<string> whitelist)
{
    private static readonly EnumerationOptions TopLevel = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private readonly Regex[] _whitelist = [.. whitelist.Select(p => ToRegex(Environment.ExpandEnvironmentVariables(p)))];

    public CleanFinding Scan(CleanTarget target, ScanProgress progress, DateTime now, CancellationToken ct)
    {
        var items = new List<CleanItem>();
        var cutoff = target.OlderThanDays > 0 ? now.AddDays(-target.OlderThanDays) : DateTime.MaxValue;
        var top = BaseFolder(target.Path);
        foreach (var root in Expand(Environment.ExpandEnvironmentVariables(target.Path)))
        {
            // A cache root that is a junction/symlink, or sits under one, would list another
            // folder's contents under the cache's name; do not descend into it. A protected
            // root (a personal folder protects itself, not its contents) is not a cache.
            if (IsWhitelisted(root) || !Directory.Exists(root) || rules.IsProtected(root) || ThroughLink(root, top)) continue;
            foreach (var path in Candidates(root, target))
            {
                ct.ThrowIfCancellationRequested();
                if (IsWhitelisted(path) || rules.IsProtected(path)) continue;
                if (Measured(path, cutoff, progress, ct) is { } item) items.Add(item);
            }
        }
        return new CleanFinding(target, items);
    }

    // One item, or null when it is too recent or vanished or became unreadable since it was
    // listed (one such item must not fail the whole target).
    private CleanItem? Measured(string path, DateTime cutoff, ScanProgress progress, CancellationToken ct)
    {
        try
        {
            var isDirectory = Directory.Exists(path);
            var written = isDirectory ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);
            if (written >= cutoff) return null;
            return new CleanItem(path, isDirectory ? folders.Measure(path, progress, ct) : FileSize(path, progress));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // The folder a rule starts from: its leading %VARIABLE% expanded ("%LOCALAPPDATA%\x" ->
    // C:\Users\me\AppData\Local). Links at or above it are the user's own layout and are
    // followed; null for a rule with no leading variable, so every folder is checked.
    private static string? BaseFolder(string rulePath)
    {
        var end = rulePath.StartsWith('%') ? rulePath.IndexOf('%', 1) : -1;
        return end > 0 ? Path.TrimEndingDirectorySeparator(Environment.ExpandEnvironmentVariables(rulePath[..(end + 1)])) : null;
    }

    // True when the root or any folder between it and the rule's base folder is a link.
    private static bool ThroughLink(string root, string? top)
    {
        if (IsReparsePoint(root)) return true;
        for (var at = Path.GetDirectoryName(root); !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
        {
            if (top is not null && at.Equals(top, StringComparison.OrdinalIgnoreCase)) return false;
            if (IsReparsePoint(at)) return true;
        }
        return false;
    }

    public bool IsWhitelisted(string path) => _whitelist.Any(w => w.IsMatch(path.TrimEnd('\\')));

    // A path that can't be read is treated as a reparse point, so a scan never descends into it.
    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }

    private static IEnumerable<string> Candidates(string root, CleanTarget target) =>
        target.Mode == CleanMode.Files
            ? Directory.EnumerateFiles(root, target.Filter, TopLevel)
            : Directory.EnumerateFileSystemEntries(root, "*", TopLevel);

    private static long FileSize(string path, ScanProgress progress)
    {
        var length = new FileInfo(path).Length;
        progress.AddFile(length);
        return length;
    }

    // "C:\a\*\b" to every existing match, one "*" segment at a time.
    private static IEnumerable<string> Expand(string path)
    {
        var star = path.IndexOf('*', StringComparison.Ordinal);
        if (star < 0) return [path];
        var parent = Path.GetDirectoryName(path[..star]) ?? "";
        var segmentEnd = path.IndexOf('\\', star);
        var pattern = segmentEnd < 0 ? path[(parent.Length + 1)..] : path[(parent.Length + 1)..segmentEnd];
        var rest = segmentEnd < 0 ? "" : path[segmentEnd..];
        if (!Directory.Exists(parent)) return [];
        return Directory.EnumerateDirectories(parent, pattern, TopLevel).SelectMany(d => Expand(d + rest));
    }

    // A whitelist entry covers itself and everything inside it; "*" stays within one segment.
    private static Regex ToRegex(string pattern) =>
        new("^" + Regex.Escape(pattern.TrimEnd('\\')).Replace(@"\*", @"[^\\]*", StringComparison.Ordinal) + @"(\\.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
