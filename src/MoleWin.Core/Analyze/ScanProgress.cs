namespace MoleWin.Core.Analyze;

// Live counters shared by the parallel measurers; the UI reads them on a timer.
public sealed class ScanProgress
{
    private long _files;
    private long _folders;
    private long _bytes;
    private string? _currentFolder;

    public long Files => Interlocked.Read(ref _files);
    public long Folders => Interlocked.Read(ref _folders);
    public long Bytes => Interlocked.Read(ref _bytes);
    public string? CurrentFolder => Volatile.Read(ref _currentFolder);

    internal void AddFile(long length)
    {
        Interlocked.Increment(ref _files);
        Interlocked.Add(ref _bytes, length);
    }

    internal void AddFolder(string path)
    {
        Interlocked.Increment(ref _folders);
        Volatile.Write(ref _currentFolder, path);
    }
}
