namespace DiskSteward.Core.Safety;

// Paths read from the registry (install folders, icons) are only touched when they are a plain
// "X:\..." path on a drive that is not mapped to a share. Opening a share path, even to check
// it exists, makes Windows sign in to that server with the user's credentials.
public static class LocalPath
{
    public static bool IsOnLocalDrive(string path)
    {
        if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] is not ('\\' or '/')) return false;
        try
        {
            return new DriveInfo(path[..1]).DriveType != DriveType.Network;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
