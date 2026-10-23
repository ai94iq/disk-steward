using DiskSteward.Core.Analyze;
using DiskSteward.Core.Clean;
using DiskSteward.Core.Safety;
using NSubstitute;

namespace DiskSteward.Core.Tests.Clean;

public sealed class CleanScannerTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Local);
    private readonly string _root = Directory.CreateTempSubdirectory("disksteward-clean-").FullName;

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
    public void A_junction_in_a_folder_above_the_cache_is_not_followed()
    {
        // <root>\app is a junction to unrelated content that happens to hold a "cache" folder.
        File(Path.Combine("elsewhere", "cache", "notes.txt"), 5);
        var link = Path.Combine(_root, "app");
        if (!TestJunctions.TryCreate(link, Path.Combine(_root, "elsewhere")))
        {
            Assert.Skip("Could not create a directory junction on this machine.");
            return;
        }
        try
        {
            var finding = Scan(Scanner(), new CleanTarget("t", CleanCategory.Apps, Path.Combine(link, "cache")));

            Assert.Empty(finding.Items);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void A_rule_whose_folder_is_protected_lists_nothing()
    {
        // A personal folder is protected itself but not its contents, so a rule aimed at the
        // folder would otherwise offer everything in it.
        File(Path.Combine("docs", "letter.docx"), 5);
        var docs = Path.Combine(_root, "docs");
        var scanner = new CleanScanner(new FolderScanner(),
            new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [], @"Z:\ProgramData", @"Z:\Users\me", @"Z:\own", [docs])), []);

        var finding = Scan(scanner, new CleanTarget("t", CleanCategory.Apps, docs));

        Assert.Empty(finding.Items);
    }

    [Fact]
    public void An_item_that_cannot_be_measured_is_skipped_and_the_rest_listed()
    {
        // A folder that vanishes between listing and measuring must not fail the whole target.
        File(Path.Combine("cache", "gone", "a.bin"), 5);
        File(Path.Combine("cache", "kept", "b.bin"), 7);
        var gone = Path.Combine(_root, "cache", "gone");
        var folders = Substitute.For<IFolderScanner>();
        folders.Measure(Arg.Any<string>(), Arg.Any<ScanProgress>(), Arg.Any<CancellationToken>()).Returns(7L);
        folders.Measure(gone, Arg.Any<ScanProgress>(), Arg.Any<CancellationToken>()).Returns(_ => throw new DirectoryNotFoundException());
        var scanner = new CleanScanner(folders,
            new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [], @"Z:\ProgramData", @"Z:\Users\me", @"Z:\own", [])), []);

        var finding = Scan(scanner, new CleanTarget("t", CleanCategory.Apps, Path.Combine(_root, "cache")));

        Assert.EndsWith("kept", Assert.Single(finding.Items).Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Age_limit_counts_a_recent_file_deep_inside_a_folder()
    {
        // A folder's own date changes only when its direct children do, so a file written
        // yesterday two levels down leaves "job" looking a month old.
        File(Path.Combine("tmp", "job", "deep", "in-use.log"), 5, ageDays: 1);
        Directory.SetLastWriteTime(Path.Combine(_root, "tmp", "job", "deep"), Now.AddDays(-30));
        Directory.SetLastWriteTime(Path.Combine(_root, "tmp", "job"), Now.AddDays(-30));

        var finding = Scan(Scanner(), new CleanTarget("t", CleanCategory.System, Path.Combine(_root, "tmp"), OlderThanDays: 7));

        Assert.Empty(finding.Items);
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
    public void Old_chat_transcripts_go_but_memory_and_recent_chats_stay()
    {
        File(Path.Combine("projects", "repo-a", "old.jsonl"), 10, ageDays: 40);
        File(Path.Combine("projects", "repo-a", "new.jsonl"), 10, ageDays: 2);
        File(Path.Combine("projects", "repo-a", "memory", "MEMORY.md"), 10, ageDays: 400);
        var chats = new CleanTarget("claude-chats", CleanCategory.AiTools, Path.Combine(_root, "projects", "*"), CleanMode.Files, "*.jsonl", 30);

        var finding = Scan(Scanner(Path.Combine(_root, "projects", "*", "memory")), chats);

        Assert.EndsWith("old.jsonl", Assert.Single(finding.Items).Path, StringComparison.Ordinal);
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
    public void Default_whitelist_keeps_claude_code_memory_but_not_its_chats()
    {
        var scanner = new CleanScanner(new FolderScanner(),
            new ProtectedPaths(new ProtectedRoots(@"Z:\W", [], @"Z:\P", @"Z:\U", @"Z:\O", [])), CleanRules.Whitelist);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var project = Path.Combine(home, ".claude", "projects", "C--work-app");

        Assert.True(scanner.IsWhitelisted(Path.Combine(project, "memory")));
        Assert.True(scanner.IsWhitelisted(Path.Combine(project, "memory", "notes.md")));
        Assert.False(scanner.IsWhitelisted(Path.Combine(project, "chat.jsonl")));
    }

    [Fact]
    public void A_whitelisted_folder_is_never_listed_by_a_rule_that_reaches_it()
    {
        // The shipped claude-chats rule lists files only, so it never reaches memory; this pins
        // the whitelist itself for any folder-level rule over the same tree.
        File(Path.Combine("projects", "app", "memory", "notes.md"), 5, ageDays: 60);
        File(Path.Combine("projects", "app", "old.jsonl"), 7, ageDays: 60);
        var scanner = Scanner(Path.Combine(_root, "projects", "*", "memory"));

        var finding = Scan(scanner, new CleanTarget("t", CleanCategory.AiTools, Path.Combine(_root, "projects", "*")));

        Assert.EndsWith("old.jsonl", Assert.Single(finding.Items).Path, StringComparison.Ordinal);
    }
}
