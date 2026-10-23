using System.Diagnostics;
using DiskSteward.Core.Safety;
using NSubstitute;

namespace DiskSteward.Core.Tests.Safety;

public sealed class SafeRemoverTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("disksteward-test-").FullName;
    private readonly IRecycleBin _bin = Substitute.For<IRecycleBin>();
    private readonly HashSet<string> _refusedByWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _leftByWindows = new(StringComparer.OrdinalIgnoreCase);
    private bool _windowsFails;

    public SafeRemoverTests() =>
        _bin.When(b => b.MoveToRecycleBin(Arg.Any<IReadOnlyList<string>>())).Do(c => FakeRecycle((IReadOnlyList<string>)c[0]!));

    // Stands in for Windows: removes what it is given, or fails the whole call as SHFileOperation
    // does when one item cannot go. _leftByWindows items are kept although the call succeeds.
    private void FakeRecycle(IReadOnlyList<string> paths)
    {
        if (_windowsFails || paths.Any(_refusedByWindows.Contains)) throw new IOException("in use");
        foreach (var path in paths.Where(p => !_leftByWindows.Contains(p)))
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
    }

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

    // A path with no links resolves to itself, so these stay deterministic without the OS.
    private SafeRemover Remover(params string[] protectedRoots) => Remover(p => p, protectedRoots);

    private SafeRemover Remover(Func<string, string?> resolve, string[] protectedRoots) => new(
        new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [], @"Z:\ProgramData", @"Z:\Users\me", @"Z:\own", protectedRoots)),
        _bin,
        isRecyclableDrive: _ => true,
        resolveRealPath: resolve);

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
        var remover = new SafeRemover(new ProtectedPaths(new ProtectedRoots(@"Z:\W", [], @"Z:\P", @"Z:\U", @"Z:\O", [])), _bin, _ => false, resolveRealPath: p => p);

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
        _refusedByWindows.Add(bad);

        var results = Remover().Recycle([bad, good]);

        Assert.Equal(RemovalOutcome.Failed, results.Single(r => r.Path == bad).Outcome);
        Assert.Equal(RemovalOutcome.Recycled, results.Single(r => r.Path == good).Outcome);
    }

    [Fact]
    public void An_item_still_there_after_the_batch_is_reported_failed()
    {
        var gone = Make("gone");
        var stuck = Make("stuck");
        _leftByWindows.Add(stuck);

        var results = Remover().Recycle([gone, stuck]);

        Assert.Equal(RemovalOutcome.Recycled, results.Single(r => r.Path == gone).Outcome);
        Assert.Equal(RemovalOutcome.Failed, results.Single(r => r.Path == stuck).Outcome);
    }

    [Fact]
    public void A_path_whose_real_target_is_protected_is_refused()
    {
        // A cache path that looks harmless but resolves (through a junction) into a protected
        // area. Z:\prot is a with-contents protected root (ProgramFiles slot).
        var cache = Make("cache");
        var remover = new SafeRemover(
            new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [@"Z:\prot"], @"Z:\P", @"Z:\U", @"Z:\own", [])),
            _bin, _ => true, resolveRealPath: _ => @"Z:\prot\inside");

        var results = remover.Recycle([cache]);

        Assert.Equal(RemovalOutcome.Protected, Assert.Single(results).Outcome);
        Assert.Empty(Binned());
    }

    [Fact]
    public void A_path_that_changes_after_a_failed_batch_is_checked_again_before_its_own_attempt()
    {
        // Harmless when the batch is built, protected by the time it gets its own attempt.
        var cache = Make("cache");
        var checks = 0;
        var remover = new SafeRemover(
            new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [@"Z:\prot"], @"Z:\P", @"Z:\U", @"Z:\own", [])),
            _bin, _ => true, resolveRealPath: p => ++checks == 1 ? p : @"Z:\prot\inside");
        _windowsFails = true;

        var results = remover.Recycle([cache]);

        Assert.Equal(RemovalOutcome.Protected, Assert.Single(results).Outcome);
        Assert.Single(_bin.ReceivedCalls());
    }

    [Fact]
    public void Folders_above_a_path_cannot_be_moved_while_it_is_checked_and_recycled()
    {
        // Another process tries to swap the folder above the item (for a junction, say) after
        // the check; Windows would follow the new folder when it recycles the item.
        var parent = Make("parent");
        var item = Make(Path.Combine("parent", "item"));
        Exception? moveError = null;
        var remover = Remover(p =>
        {
            try { Directory.Move(parent, parent + "-moved"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { moveError = ex; }
            return p;
        }, []);

        remover.Recycle([item]);

        Assert.NotNull(moveError);
        Assert.True(Directory.Exists(parent));
    }

    [Fact]
    public void A_path_whose_real_target_cannot_be_resolved_is_refused()
    {
        var cache = Make("cache");
        var remover = Remover(_ => null, []);

        var results = remover.Recycle([cache]);

        Assert.Equal(RemovalOutcome.Failed, Assert.Single(results).Outcome);
        Assert.Empty(Binned());
    }

    [Fact]
    public void A_real_junction_into_a_protected_target_is_refused_end_to_end()
    {
        // The whole chain with a real junction and the real OS resolver (no injected fake).
        var protectedTarget = Make("protected-target");
        File.WriteAllText(Path.Combine(protectedTarget, "keep.txt"), "x");
        var cache = Path.Combine(_root, "cache"); // a junction that aims the cache at the protected dir
        if (!TryCreateJunction(cache, protectedTarget))
        {
            Assert.Skip("Could not create a directory junction on this machine.");
            return;
        }
        try
        {
            // protectedTarget is a with-contents protected root (ProgramFiles slot).
            var remover = new SafeRemover(
                new ProtectedPaths(new ProtectedRoots(@"Z:\W", [protectedTarget], @"Z:\P", @"Z:\U", @"Z:\O", [])),
                _bin, _ => true);

            var results = remover.Recycle([Path.Combine(cache, "keep.txt")]);

            Assert.Equal(RemovalOutcome.Protected, Assert.Single(results).Outcome);
            Assert.Empty(Binned());
            Assert.True(File.Exists(Path.Combine(protectedTarget, "keep.txt")));
        }
        finally
        {
            // Remove the junction link itself (not its target) before the recursive teardown
            // so Dispose does not walk into the real folder.
            Directory.Delete(cache);
        }
    }

    private static bool TryCreateJunction(string link, string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            process!.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            return false;
        }
    }
}
