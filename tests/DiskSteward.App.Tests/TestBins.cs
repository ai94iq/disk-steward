using DiskSteward.Core.Safety;
using NSubstitute;

namespace DiskSteward.App.Tests;

public static class TestBins
{
    // A Recycle Bin stand-in that removes what it is given, as Windows does, so SafeRemover
    // (which checks the disk after the call) reports the items recycled. Calls stay recorded.
    public static IRecycleBin ThatRemoves()
    {
        var bin = Substitute.For<IRecycleBin>();
        bin.When(b => b.MoveToRecycleBin(Arg.Any<IReadOnlyList<string>>())).Do(c =>
        {
            foreach (var path in (IReadOnlyList<string>)c[0]!)
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                else if (File.Exists(path)) File.Delete(path);
            }
        });
        return bin;
    }
}
