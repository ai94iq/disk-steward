using System.Text.RegularExpressions;
using MoleWin.Core.Analyze;
using MoleWin.Core.Safety;

namespace MoleWin.Core.Clean;

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
        foreach (var root in Expand(Environment.ExpandEnvironmentVariables(target.Path)))
        {
            // A cache root that is itself a junction/symlink would list the real target's
            // contents under the cache's name; do not descend into it. (SafeRemover also
            // refuses such paths, but this keeps them out of the list the user sees.)
            if (IsWhitelisted(root) || !Directory.Exists(root) || IsReparsePoint(root)) continue;
            foreach (var path in Candidates(root, target))
            {
                ct.ThrowIfCancellationRequested();
                if (IsWhitelisted(path) || rules.IsProtected(path)) continue;
                var isDirectory = Directory.Exists(path);
                var written = isDirectory ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);
                if (written >= cutoff) continue;
                var size = isDirectory ? folders.Measure(path, progress, ct) : FileSize(path, progress);
                items.Add(new CleanItem(path, size));
            }
        }
        return new CleanFinding(target, items);
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
