namespace DiskSteward.Core.Apps;

// DataFolders: where apps keep per-user data (AppData Roaming, Local, LocalLow, Local\Programs).
// ShortcutFolders: Start Menu Programs folders.
public sealed record LeftoverRoots(IReadOnlyList<string> DataFolders, IReadOnlyList<string> ShortcutFolders)
{
    public static LeftoverRoots ForCurrentUser()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new LeftoverRoots(
            [
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                local,
                Path.Combine(local, "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow"),
            ],
            [Environment.GetFolderPath(Environment.SpecialFolder.Programs)]);
    }
}
