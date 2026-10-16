using System.IO.Enumeration;
using System.Runtime.InteropServices;

namespace MoleWin.Core.Analyze;

// Read-only: lists and measures, never changes anything. Links are skipped so a junction
// cannot count a folder twice or lead the walk outside the folder being measured, and
// folders the user may not read are skipped instead of failing the scan.
// Sizes are space on disk: sparse and compressed files (VM and WSL disk images) report a
// logical length far above what they occupy, so those ask Windows for the allocated size.
public sealed class FolderScanner : IFolderScanner
{
    private const FileAttributes NotFullyAllocated = FileAttributes.SparseFile | FileAttributes.Compressed;

    private static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false,
    };

    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false,
        RecurseSubdirectories = true,
    };

    public IReadOnlyList<FolderEntry> ListRoots() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => new FolderEntry(d.Name, d.RootDirectory.FullName, true, d.TotalSize - d.TotalFreeSpace, d.TotalSize))
            .ToList();

    public IReadOnlyList<FolderEntry> ListChildren(string folder)
    {
        var entries = new FileSystemEnumerable<FolderEntry>(
            folder,
            (ref FileSystemEntry e) => new FolderEntry(
                e.FileName.ToString(), e.ToFullPath(), e.IsDirectory, e.IsDirectory ? null : SizeOnDisk(ref e),
                IsSystem: (e.Attributes & FileAttributes.System) != 0),
            Options);
        return entries.ToList();
    }

    public long Measure(string folder, ScanProgress progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        progress.AddFolder(folder);
        var entries = new FileSystemEnumerable<long>(
            folder,
            (ref FileSystemEntry e) =>
            {
                if (e.IsDirectory)
                {
                    progress.AddFolder(e.ToFullPath());
                    return 0;
                }
                var size = SizeOnDisk(ref e);
                progress.AddFile(size);
                return size;
            },
            RecursiveOptions);

        long total = 0;
        foreach (var length in entries)
        {
            ct.ThrowIfCancellationRequested();
            total += length;
        }
        return total;
    }

    private static long SizeOnDisk(ref FileSystemEntry e)
    {
        if ((e.Attributes & NotFullyAllocated) == 0 || !OperatingSystem.IsWindows()) return e.Length;
        var low = GetCompressedFileSizeW(e.ToFullPath(), out var high);
        // INVALID_FILE_SIZE with an error: fall back to the logical length rather than drop the file.
        return low == uint.MaxValue && Marshal.GetLastPInvokeError() != 0 ? e.Length : ((long)high << 32) | low;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetCompressedFileSizeW(string fileName, out uint fileSizeHigh);
}
