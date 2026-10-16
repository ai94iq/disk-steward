using MoleWin.Core.Safety;
using NSubstitute;

namespace MoleWin.Core.Tests.Safety;

public sealed class SafeRemoverTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("molewin-test-").FullName;
    private readonly IRecycleBin _bin = Substitute.For<IRecycleBin>();

    // Every path handed to the Recycle Bin, across batched calls.
    private string[] Binned() =>
        [.. _bin.ReceivedCalls().SelectMany(c => (IReadOnlyList<string>)c.GetArguments()[0]!)];

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_path_is_gone_when_it_or_a_folder_above_it_went()
    {
        RemovalResult[] results =
        [
            new(@"C:\a", RemovalOutcome.Recycled),
            new(@"C:\b", RemovalOutcome.Failed, "busy"),
            new(@"C:\c", RemovalOutcome.Missing),
        ];

        var gone = SafeRemover.GoneCheck(results);

        Assert.True(gone(@"C:\a\x.tmp"));
        Assert.True(gone(@"C:\c"));
        Assert.False(gone(@"C:\b\x.tmp"));
        Assert.False(gone(@"C:\ab"));
    }

    private SafeRemover Remover(params string[] protectedRoots) => new(
        new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [], @"Z:\ProgramData", @"Z:\Users\me", @"Z:\own", protectedRoots)),
        _bin,
        isRecyclableDrive: _ => true);

    private string Make(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Existing_paths_go_to_the_recycle_bin()
    {
        var cache = Make("cache");

        var results = Remover().Recycle([cache]);

        Assert.Contains(cache, Binned());
        Assert.Equal(RemovalOutcome.Recycled, Assert.Single(results).Outcome);
    }

    [Fact]
    public void Protected_paths_are_refused_even_when_asked()
    {
        var keep = Make("keep");

        var results = Remover(keep).Recycle([keep]);

        Assert.Empty(Binned());
        Assert.Equal(RemovalOutcome.Protected, Assert.Single(results).Outcome);
    }

    [Fact]
    public void Paths_on_drives_without_a_recycle_bin_are_refused()
    {
        var cache = Make("cache");
        var remover = new SafeRemover(new ProtectedPaths(new ProtectedRoots(@"Z:\W", [], @"Z:\P", @"Z:\U", @"Z:\O", [])), _bin, _ => false);

        var results = remover.Recycle([cache]);

        Assert.Empty(Binned());
        Assert.Equal(RemovalOutcome.NotRecyclable, Assert.Single(results).Outcome);
    }

    [Fact]
    public void Children_of_a_selected_folder_are_not_removed_twice()
    {
        var parent = Make("parent");
        var child = Make(Path.Combine("parent", "child"));

        var results = Remover().Recycle([child, parent]);

        Assert.Contains(parent, Binned());
        Assert.DoesNotContain(child, Binned());
        Assert.Single(results);
    }

    [Fact]
    public void Many_paths_go_to_the_recycle_bin_in_one_operation()
    {
        var paths = Enumerable.Range(0, 50).Select(i => Make($"cache{i}")).ToArray();

        var results = Remover().Recycle(paths);

        Assert.Single(_bin.ReceivedCalls());
        Assert.All(results, r => Assert.Equal(RemovalOutcome.Recycled, r.Outcome));
    }

    [Fact]
    public void Missing_paths_are_reported_not_failed()
    {
        var results = Remover().Recycle([Path.Combine(_root, "gone")]);

        Assert.Equal(RemovalOutcome.Missing, Assert.Single(results).Outcome);
    }

    [Fact]
    public void A_recycle_failure_is_reported_and_the_rest_continue()
    {
        var bad = Make("bad");
        var good = Make("good");
        _bin.When(b => b.MoveToRecycleBin(Arg.Is<IReadOnlyList<string>>(l => l.Contains(bad)))).Do(_ => throw new IOException("in use"));

        var results = Remover().Recycle([bad, good]);

        Assert.Equal(RemovalOutcome.Failed, results.Single(r => r.Path == bad).Outcome);
        Assert.Equal(RemovalOutcome.Recycled, results.Single(r => r.Path == good).Outcome);
    }
}
