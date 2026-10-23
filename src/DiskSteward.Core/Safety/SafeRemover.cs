namespace DiskSteward.Core.Safety;

// The only way the app removes anything. It takes a list the user confirmed, re-checks each
// path at the moment of removal, and recycles; it never deletes permanently.
public sealed class SafeRemover(ProtectedPaths rules, IRecycleBin bin, Func<string, bool> isRecyclableDrive, Func<string, string?>? resolveRealPath = null)
{
    // Resolves junctions and symlinks to the real target. The default asks Windows; tests pass
    // their own. A path the OS cannot open to resolve is treated as "do not touch".
    private readonly Func<string, string?> _resolveRealPath = resolveRealPath ?? LinkResolver.RealPath;

    // Every path that passes the checks goes to Windows in one operation, which is what keeps a
    // few thousand cache files from taking minutes. If that operation fails part way, Windows
    // does not say which item stopped it, so each item still there is checked again and gets
    // its own attempt and its own reason. The folders above every path are held for the whole
    // call, so what was checked is what Windows removes.
    public IReadOnlyList<RemovalResult> Recycle(IEnumerable<string> paths)
    {
        var results = new List<RemovalResult>();
        var eligible = new List<string>();
        var kept = WithoutNestedPaths(paths);
        using var pins = FolderPins.Hold(kept.SelectMany(FoldersAbove));
        foreach (var path in kept)
        {
            if (Refusal(path, pins) is { } refused) results.Add(refused);
            else eligible.Add(path);
        }
        if (eligible.Count == 0) return results;

        try
        {
            bin.MoveToRecycleBin(eligible);
            // Windows can report success for a list it did not finish; trust what is left on disk.
            results.AddRange(eligible.Select(p => Exists(p)
                ? new RemovalResult(p, RemovalOutcome.Failed, "Still there after recycling.")
                : new RemovalResult(p, RemovalOutcome.Recycled)));
        }
        catch (Exception ex) when (IsRecycleError(ex))
        {
            results.AddRange(eligible.Select(p => !Exists(p) ? new RemovalResult(p, RemovalOutcome.Recycled)
                : Refusal(p, pins) ?? RecycleOne(p)));
        }
        return results;
    }

    // Why a path may not go, checked while the folders above it are held; null when it may. The
    // lexical path is checked first, then its real target: a junction or symlink at the path or
    // any folder above it can aim a harmless-looking cache path into a protected area or a
    // network share, which the lexical check cannot see.
    private RemovalResult? Refusal(string path, FolderPins pins)
    {
        if (rules.IsProtected(path)) return new RemovalResult(path, RemovalOutcome.Protected);
        if (!Exists(path)) return new RemovalResult(path, RemovalOutcome.Missing);
        if (!FoldersAbove(path).All(pins.IsHeld))
            return new RemovalResult(path, RemovalOutcome.Failed, "Could not hold the folders above it.");

        var real = _resolveRealPath(path);
        if (real is null) return new RemovalResult(path, RemovalOutcome.Failed, "Could not resolve the real path.");
        if (rules.IsProtected(real)) return new RemovalResult(path, RemovalOutcome.Protected);

        // Network shares and removable drives have no Recycle Bin: Windows would delete for good.
        if (!isRecyclableDrive(real)) return new RemovalResult(path, RemovalOutcome.NotRecyclable);
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

    // Each folder above the path, up to and including the drive root ("C:\", not "C:").
    private static IEnumerable<string> FoldersAbove(string path)
    {
        for (var at = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)); !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
            yield return at;
    }

    // The path, then each folder above it up to the drive root.
    private static IEnumerable<string> SelfOrAbove(string path)
    {
        for (var at = path; !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at)) yield return Key(at);
    }

    private static string Key(string path) => path.TrimEnd('\\', '/');
}
