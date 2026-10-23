namespace DiskSteward.Core.Safety;

// The folders ProtectedPaths guards. Windows, Program Files, ProgramData and the app's own
// data are protected with everything inside them; the profile and KnownFolders are protected
// themselves, while their contents stay removable.
public sealed record ProtectedRoots(
    string Windows,
    IReadOnlyList<string> ProgramFiles,
    string ProgramData,
    string UserProfile,
    string AppOwnData,
    IReadOnlyList<string> KnownFolders)
{
    public static ProtectedRoots ForCurrentUser(string appOwnData)
    {
        static string Folder(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
        var profile = Folder(Environment.SpecialFolder.UserProfile);
        string[] known =
        [
            Folder(Environment.SpecialFolder.Desktop),
            Folder(Environment.SpecialFolder.MyDocuments),
            Folder(Environment.SpecialFolder.MyPictures),
            Folder(Environment.SpecialFolder.MyMusic),
            Folder(Environment.SpecialFolder.MyVideos),
            Folder(Environment.SpecialFolder.Favorites),
            Folder(Environment.SpecialFolder.ApplicationData),
            Folder(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(profile, "AppData", "LocalLow"),
            Path.Combine(profile, "Downloads"),
            Environment.GetEnvironmentVariable("OneDrive") ?? "",
            // Public and every other profile on the machine: other local users can write in
            // them, and recycling one whole would take someone else's files.
            .. OtherProfiles(profile),
        ];
        return new ProtectedRoots(
            Folder(Environment.SpecialFolder.Windows),
            [Folder(Environment.SpecialFolder.ProgramFiles), Folder(Environment.SpecialFolder.ProgramFilesX86)],
            Folder(Environment.SpecialFolder.CommonApplicationData),
            profile,
            appOwnData,
            [.. known.Where(k => k.Length > 0)]);
    }

    private static IEnumerable<string> OtherProfiles(string profile)
    {
        var profiles = Path.GetDirectoryName(profile);
        if (string.IsNullOrEmpty(profiles) || !Directory.Exists(profiles)) return [];
        try
        {
            return Directory.EnumerateDirectories(profiles, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 }).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
