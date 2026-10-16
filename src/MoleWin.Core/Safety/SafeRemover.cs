namespace MoleWin.Core.Safety;

// The only way the app removes anything. It takes a list the user confirmed, re-checks each
// path at the moment of removal, and recycles; it never deletes permanently.
public sealed class SafeRemover(ProtectedPaths rules, IRecycleBin bin, Func<string, bool> isRecyclableDrive)
{
    // Every path that passes the checks goes to Windows in one operation, which is what keeps a
    // few thousand cache files from taking minutes. If that operation fails part way, Windows
    // does not say which item stopped it, so each item still there gets its own attempt and
    // its own reason.
    public IReadOnlyList<RemovalResult> Recycle(IEnumerable<string> paths)
    {
        var results = new List<RemovalResult>();
        var eligible = new List<string>();
        foreach (var path in WithoutNestedPaths(paths))
        {
            if (Refusal(path) is { } refused) results.Add(refused);
            else eligible.Add(path);
        }
        if (eligible.Count == 0) return results;

        try
        {
            bin.MoveToRecycleBin(eligible);
            results.AddRange(eligible.Select(p => new RemovalResult(p, RemovalOutcome.Recycled)));
        }
        catch (Exception ex) when (IsRecycleError(ex))
        {
            results.AddRange(eligible.Select(p => Exists(p) ? RecycleOne(p) : new RemovalResult(p, RemovalOutcome.Recycled)));
        }
        return results;
    }

    // Why a path may not go, re-checked at the moment of removal; null when it may.
    private RemovalResult? Refusal(string path)
    {
        if (rules.IsProtected(path)) return new RemovalResult(path, RemovalOutcome.Protected);
        if (!Exists(path)) return new RemovalResult(path, RemovalOutcome.Missing);
        // Network shares and removable drives have no Recycle Bin: Windows would delete for good.
        if (!isRecyclableDrive(path)) return new RemovalResult(path, RemovalOutcome.NotRecyclable);
        return null;
    }

    private RemovalResult RecycleOne(string path)
    {
        try
        {
            bin.MoveToRecycleBin([path]);
            return new RemovalResult(path, RemovalOutcome.Recycled);
        }
        catch (Exception ex) when (IsRecycleError(ex))
        {
            return new RemovalResult(path, RemovalOutcome.Failed, ex.Message);
        }
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static bool IsRecycleError(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidOperationException;

    // A test for "is this path no longer there": it, or a folder above it, was recycled or was
    // already gone. Lets a screen drop removed rows without scanning again. A set lookup per
    // folder level, so checking thousands of paths against thousands of results stays fast.
    public static Func<string, bool> GoneCheck(IEnumerable<RemovalResult> results)
    {
        var gone = results.Where(r => r.Outcome is RemovalOutcome.Recycled or RemovalOutcome.Missing)
            .Select(r => Key(r.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return path => SelfOrAbove(path).Any(gone.Contains);
    }

    // A folder and something inside it: recycling the folder covers both. Shortest first, so a
    // folder is kept before anything under it is checked against it.
    private static List<string> WithoutNestedPaths(IEnumerable<string> paths)
    {
        var keptKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Length))
        {
            if (SelfOrAbove(path).Skip(1).Any(keptKeys.Contains)) continue;
            keptKeys.Add(Key(path));
            kept.Add(path);
        }
        return kept;
    }

    // The path, then each folder above it up to the drive root.
    private static IEnumerable<string> SelfOrAbove(string path)
    {
        for (var at = path; !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at)) yield return Key(at);
    }

    private static string Key(string path) => path.TrimEnd('\\', '/');
}
