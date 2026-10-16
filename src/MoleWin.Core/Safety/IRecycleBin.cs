namespace MoleWin.Core.Safety;

public interface IRecycleBin
{
    // Moves one file or folder to the Recycle Bin; throws when Windows did not recycle it.
    void MoveToRecycleBin(string path);
}
