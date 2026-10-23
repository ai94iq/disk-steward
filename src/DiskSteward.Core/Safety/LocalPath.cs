namespace DiskSteward.Core.Safety;

// Paths read from the registry (install folders, icons) are only touched when they are a plain
// "X:\..." path on a drive Windows reports as local (fixed, removable, RAM or optical). Opening a share path, even to check
// it exists, makes Windows sign in to that server with the user's credentials.
public static class LocalPath
{
    public static bool IsOnLocalDrive(string path)
    {
        if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] is not ('\\' or '/')) return false;
        try
        {
            // Only drive types known to be local; a letter with nothing behind it, or one Windows
            // cannot classify, may be a disconnected share.
            return new DriveInfo(path[..1]).DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Ram or DriveType.CDRom;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
