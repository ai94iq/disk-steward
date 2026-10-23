using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DiskSteward.Core.Safety;

// Holds folders open so no other process can rename, delete or replace them until disposed.
// Windows resolves a path again each time it is used, so a folder above a checked path could
// otherwise be swapped for a junction between the check and the recycle, and the recycle would
// follow it. Each handle lists the folder but does not share delete, which is what a rename or
// delete needs; work inside the folder (recycling an item in it) is not affected.
public sealed class FolderPins : IDisposable
{
    private const uint FileListDirectory = 0x1;
    private const uint FileReadAttributes = 0x80;
    private const uint ShareRead = 0x1;
    private const uint ShareReadWrite = 0x1 | 0x2; // no FILE_SHARE_DELETE
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000; // lets CreateFile open a directory
    private const uint FileFlagOpenReparsePoint = 0x00200000; // holds a junction itself, not its target

    private readonly Dictionary<string, SafeFileHandle> _held = new(StringComparer.OrdinalIgnoreCase);

    private FolderPins() { }

    // Holds every folder it can; IsHeld says which ones it got.
    public static FolderPins Hold(IEnumerable<string> folders)
    {
        var pins = new FolderPins();
        pins.Add(folders);
        return pins;
    }

    // Holds more folders. A junction or symlink is held against writes as well, because
    // retargeting one needs only write access, not delete; its target's folders are not held
    // by this and need their own Add.
    public void Add(IEnumerable<string> folders)
    {
        foreach (var folder in folders)
        {
            if (_held.ContainsKey(folder)) continue;
            var handle = CreateFileW(folder, FileListDirectory | FileReadAttributes, IsLink(folder) ? ShareRead : ShareReadWrite,
                IntPtr.Zero, OpenExisting, FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
            if (handle.IsInvalid) handle.Dispose();
            else _held[folder] = handle;
        }
    }

    private static bool IsLink(string folder)
    {
        try { return (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public bool IsHeld(string folder) => _held.ContainsKey(folder);

    public void Dispose()
    {
        foreach (var handle in _held.Values) handle.Dispose();
        _held.Clear();
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
}
