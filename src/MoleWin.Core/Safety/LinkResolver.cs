using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MoleWin.Core.Safety;

// The real path a path points at, with every junction and symlink along it resolved by the OS
// (GetFinalPathNameByHandle), so the safety checks run against what would actually be removed
// rather than the lexical path. A directory junction can sit at a cache folder and aim it into
// a protected area or a network share; Path.GetFullPath does not see that, this does.
public static class LinkResolver
{
    private const uint FileFlagBackupSemantics = 0x02000000; // lets CreateFile open a directory
    private const uint OpenExisting = 3;
    private const uint ShareAll = 0x1 | 0x2 | 0x4;

    // The resolved path (plain form, no \\?\ prefix), or null when it cannot be opened to
    // resolve. Callers treat null as "do not touch it".
    public static string? RealPath(string path)
    {
        using var handle = CreateFileW(path, 0, ShareAll, IntPtr.Zero, OpenExisting, FileFlagBackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid) return null;

        var buffer = new StringBuilder(1024);
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0) return null;
        if (length > buffer.Capacity)
        {
            buffer = new StringBuilder((int)length);
            if (GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0) == 0) return null;
        }

        var resolved = buffer.ToString();
        if (resolved.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) return @"\\" + resolved[8..];
        if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) return resolved[4..];
        return resolved;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder buffer, uint size, uint flags);
}
