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

    // True when the path, or any folder above it, is a junction or symlink. Walks from the drive
    // root down and stops at the first link, so it never asks the OS about a path beyond a link
    // (which would follow it, possibly to a share). An unreadable component is treated as a link
    // so the caller fails closed. A path that is simply missing crosses no link.
    public static bool CrossesLink(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException) { return true; }

        var root = Path.GetPathRoot(full) ?? "";
        var parts = full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current); }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
            if ((attributes & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }
}
