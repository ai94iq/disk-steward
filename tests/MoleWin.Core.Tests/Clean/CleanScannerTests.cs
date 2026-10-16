using MoleWin.Core.Analyze;
using MoleWin.Core.Clean;
using MoleWin.Core.Safety;

namespace MoleWin.Core.Tests.Clean;

public sealed class CleanScannerTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Local);
    private readonly string _root = Directory.CreateTempSubdirectory("molewin-clean-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private CleanScanner Scanner(params string[] whitelist) => new(
        new FolderScanner(),
        new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [], @"Z:\ProgramData", @"Z:\Users\me", @"Z:\own", [])),
        whitelist);

    private string File(string relative, int bytes, int ageDays = 0)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, new byte[bytes]);
        System.IO.File.SetLastWriteTime(path, Now.AddDays(-ageDays));
        return path;
    }

    private CleanFinding Scan(CleanScanner scanner, CleanTarget target) =>
        scanner.Scan(target, new ScanProgress(), Now, TestContext.Current.CancellationToken);

    [Fact]
    public void Contents_mode_lists_each_child_with_its_size_and_keeps_the_folder()
    {
        File(Path.Combine("cache", "a.bin"), 10);
        File(Path.Combine("cache", "sub", "b.bin"), 30);
        var sub = Path.Combine(_root, "cache", "sub");
        Directory.SetLastWriteTime(sub, Now.AddDays(-30));

        var finding = Scan(Scanner(), new CleanTarget("t", CleanCategory.Apps, Path.Combine(_root, "cache")));

        Assert.Equal(40, finding.Size);
        Assert.Equal(2, finding.Items.Count);
        Assert.DoesNotContain(finding.Items, i => i.Path == Path.Combine(_root, "cache"));
    }

    [Fact]
    public void Age_limit_leaves_recent_items_alone()
    {
        File(Path.Combine("tmp", "old.tmp"), 5, ageDays: 10);
        File(Path.Combine("tmp", "new.tmp"), 7, ageDays: 1);

        var finding = Scan(Scanner(), new CleanTarget("t", CleanCategory.System, Path.Combine(_root, "tmp"), OlderThanDays: 7));

        Assert.EndsWith("old.tmp", Assert.Single(finding.Items).Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Files_mode_matches_the_filter_only()
    {
        File(Path.Combine("dumps", "app.dmp"), 5);
        File(Path.Combine("dumps", "keep.txt"), 5);

        var finding = Scan(Scanner(), new CleanTarget("t", CleanCategory.System, Path.Combine(_root, "dumps"), CleanMode.Files, "*.dmp"));

        Assert.EndsWith("app.dmp", Assert.Single(finding.Items).Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Star_segments_expand_to_every_matching_folder()
    {
        File(Path.Combine("Profiles", "abc.default", "cache2", "x"), 3);
        File(Path.Combine("Profiles", "def.dev", "cache2", "y"), 4);

        var finding = Scan(Scanner(), new CleanTarget("t", CleanCategory.Browsers, Path.Combine(_root, "Profiles", "*", "cache2")));

        Assert.Equal(7, finding.Size);
    }

    [Fact]
    public void Whitelisted_paths_are_never_items()
    {
        File(Path.Combine("cache", "models", "big.bin"), 100);
        File(Path.Combine("cache", "junk.bin"), 1);

        var finding = Scan(Scanner(Path.Combine(_root, "cache", "mod*")), new CleanTarget("t", CleanCategory.Apps, Path.Combine(_root, "cache")));

        Assert.EndsWith("junk.bin", Assert.Single(finding.Items).Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_targets_give_an_empty_finding()
    {
        var finding = Scan(Scanner(), new CleanTarget("t", CleanCategory.Apps, Path.Combine(_root, "nope")));

        Assert.Empty(finding.Items);
    }

    [Fact]
    public void Every_default_rule_has_a_unique_id_and_an_absolute_path_once_expanded()
    {
        Assert.Equal(CleanRules.Defaults.Count, CleanRules.Defaults.Select(t => t.Id).Distinct().Count());
        Assert.All(CleanRules.Defaults, t => Assert.True(Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(t.Path)), t.Id));
    }

    [Fact]
    public void Default_whitelist_covers_ollama_models_and_store_app_data()
    {
        var scanner = new CleanScanner(new FolderScanner(),
            new ProtectedPaths(new ProtectedRoots(@"Z:\W", [], @"Z:\P", @"Z:\U", @"Z:\O", [])), CleanRules.Whitelist);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.True(scanner.IsWhitelisted(Path.Combine(home, ".ollama", "models", "blobs")));
        Assert.True(scanner.IsWhitelisted(Path.Combine(local, "Packages", "Microsoft.WindowsStore_8wekyb3d8bbwe", "LocalCache")));
        Assert.False(scanner.IsWhitelisted(Path.Combine(local, "Temp")));
    }
}
