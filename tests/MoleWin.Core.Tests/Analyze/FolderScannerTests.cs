using MoleWin.Core.Analyze;

namespace MoleWin.Core.Tests.Analyze;

public sealed class FolderScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("molewin-scan-").FullName;
    private readonly FolderScanner _scanner = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void File(string relative, int bytes)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, new byte[bytes]);
    }

    [Fact]
    public void Children_list_files_with_size_and_folders_without()
    {
        File("a.bin", 10);
        File(Path.Combine("sub", "b.bin"), 20);

        var children = _scanner.ListChildren(_root);

        Assert.Equal(10, children.Single(c => c.Name == "a.bin").Size);
        var sub = children.Single(c => c.Name == "sub");
        Assert.True(sub.IsDirectory);
        Assert.Null(sub.Size);
    }

    [Fact]
    public void List_children_marks_the_system_attribute()
    {
        File("normal.bin", 1);
        File("flagged.bin", 1);
        System.IO.File.SetAttributes(Path.Combine(_root, "flagged.bin"), FileAttributes.System);

        var children = _scanner.ListChildren(_root);
        System.IO.File.SetAttributes(Path.Combine(_root, "flagged.bin"), FileAttributes.Normal);

        Assert.True(children.Single(c => c.Name == "flagged.bin").IsSystem);
        Assert.False(children.Single(c => c.Name == "normal.bin").IsSystem);
    }

    [Fact]
    public void Measure_sums_every_file_below_and_counts_progress()
    {
        File(Path.Combine("x", "1.bin"), 100);
        File(Path.Combine("x", "y", "2.bin"), 250);
        File(Path.Combine("x", "y", "z", "3.bin"), 50);
        var progress = new ScanProgress();

        var size = _scanner.Measure(Path.Combine(_root, "x"), progress, TestContext.Current.CancellationToken);

        Assert.Equal(400, size);
        Assert.Equal(3, progress.Files);
        Assert.Equal(400, progress.Bytes);
        Assert.Equal(3, progress.Folders);
        Assert.NotNull(progress.CurrentFolder);
    }

    [Fact]
    public void Sparse_files_count_their_space_on_disk_not_their_length()
    {
        var dir = Path.Combine(_root, "vm");
        Directory.CreateDirectory(dir);
        var disk = Path.Combine(dir, "disk.vhdx");
        System.IO.File.WriteAllBytes(disk, []);
        using (var fsutil = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("fsutil", ["sparse", "setflag", disk]) { CreateNoWindow = true, RedirectStandardOutput = true })!)
        {
            fsutil.WaitForExit();
        }
        using (var stream = System.IO.File.OpenWrite(disk)) stream.SetLength(1L << 30);

        var size = _scanner.Measure(dir, new ScanProgress(), TestContext.Current.CancellationToken);

        Assert.True(size < 1L << 20, $"a 1 GB sparse file with no data measured {size} bytes");
    }

    [Fact]
    public void Measure_stops_when_cancelled()
    {
        File(Path.Combine("x", "1.bin"), 1);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() => _scanner.Measure(Path.Combine(_root, "x"), new ScanProgress(), cancelled.Token));
    }
}
