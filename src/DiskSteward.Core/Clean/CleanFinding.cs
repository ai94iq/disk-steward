namespace DiskSteward.Core.Clean;

// What one target holds right now; nothing is removed by finding it.
public sealed record CleanFinding(CleanTarget Target, IReadOnlyList<CleanItem> Items)
{
    public long Size => Items.Sum(i => i.Size);
}
