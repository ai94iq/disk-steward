namespace MoleWin.Core.Safety;

public interface IRecycleBin
{
    // Moves files and folders to the Recycle Bin in one operation (one call per item costs
    // Windows about 17 ms each); throws when Windows did not recycle all of them.
    void MoveToRecycleBin(IReadOnlyList<string> paths);
}
