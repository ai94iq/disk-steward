namespace MoleWin.Core.Safety;

// The only way the app removes anything. It takes a list the user confirmed, re-checks each
// path at the moment of removal, and recycles; it never deletes permanently.
public sealed class SafeRemover(ProtectedPaths rules, IRecycleBin bin, Func<string, bool> isRecyclableDrive)
{
    public IReadOnlyList<RemovalResult> Recycle(IEnumerable<string> paths)
    {
        var results = new List<RemovalResult>();
        foreach (var path in WithoutNestedPaths(paths))
        {
            results.Add(RecycleOne(path));
        }
        return results;
    }

    private RemovalResult RecycleOne(string path)
    {
        if (rules.IsProtected(path)) return new RemovalResult(path, RemovalOutcome.Protected);
        if (!File.Exists(path) && !Directory.Exists(path)) return new RemovalResult(path, RemovalOutcome.Missing);
        // Network shares and removable drives have no Recycle Bin: Windows would delete for good.
        if (!isRecyclableDrive(path)) return new RemovalResult(path, RemovalOutcome.NotRecyclable);

        try
        {
            bin.MoveToRecycleBin(path);
            return new RemovalResult(path, RemovalOutcome.Recycled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new RemovalResult(path, RemovalOutcome.Failed, ex.Message);
        }
    }

    // True when the results say the path is no longer there: it, or a folder above it, was
    // recycled or was already gone. Lets a screen drop removed rows without scanning again.
    public static bool IsGone(IEnumerable<RemovalResult> results, string path) =>
        results.Any(r => r.Outcome is RemovalOutcome.Recycled or RemovalOutcome.Missing
            && (path.Equals(r.Path, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(r.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)));

    // A folder and something inside it: recycling the folder covers both.
    private static List<string> WithoutNestedPaths(IEnumerable<string> paths)
    {
        var sorted = paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Length).ToList();
        var kept = new List<string>();
        foreach (var path in sorted)
        {
            if (!kept.Any(k => path.StartsWith(k.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))) kept.Add(path);
        }
        return kept;
    }
}
