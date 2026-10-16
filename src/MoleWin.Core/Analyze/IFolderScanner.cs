namespace MoleWin.Core.Analyze;

public interface IFolderScanner
{
    // Where browsing starts: the fixed drives with their used space.
    IReadOnlyList<FolderEntry> ListRoots();

    // Immediate children, without measuring folders. Links (junctions, symlinks) are left out.
    IReadOnlyList<FolderEntry> ListChildren(string folder);

    // Total size of everything under a folder, reporting as it goes. Blocking; run off the UI thread.
    long Measure(string folder, ScanProgress progress, CancellationToken ct);
}
