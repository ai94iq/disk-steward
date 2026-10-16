namespace DiskSteward.Core.Clean;

// One place a cache or leftover lives. Its label is resx key Clean_Target_{Id}. Path may use %LOCALAPPDATA%-style variables and a
// "*" inside a segment (Profiles\*\cache2). OlderThanDays 0 means any age.
public sealed record CleanTarget(
    string Id,
    CleanCategory Category,
    string Path,
    CleanMode Mode = CleanMode.Contents,
    string Filter = "*",
    int OlderThanDays = 0);
