using DiskSteward.App.Features.Clean;
using DiskSteward.App.Services;
using DiskSteward.Core.Analyze;
using DiskSteward.Core.Clean;
using DiskSteward.Core.Safety;
using NSubstitute;

namespace DiskSteward.App.Tests.Clean;

public sealed class CleanViewModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("disksteward-cleanvm-").FullName;
    private readonly IRecycleBin _bin = TestBins.ThatRemoves();

    // Every path handed to the Recycle Bin, across batched calls.
    private string[] Binned() =>
        [.. _bin.ReceivedCalls().SelectMany(c => (IReadOnlyList<string>)c.GetArguments()[0]!)];
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Folder(string name, params (string File, int Bytes)[] files)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        foreach (var (file, bytes) in files) File.WriteAllBytes(Path.Combine(dir, file), new byte[bytes]);
        return dir;
    }

    private CleanViewModel Create(params CleanTarget[] targets)
    {
        var rules = new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [], @"Z:\ProgramData", @"Z:\Users\me", @"Z:\own", []));
        return new CleanViewModel(new CleanScanner(new FolderScanner(), rules, []), targets, new SafeRemover(rules, _bin, _ => true),
            _dialogs, NullLogger<CleanViewModel>.Instance) { RefreshInterval = TimeSpan.FromMilliseconds(1) };
    }

    [Fact]
    public async Task Opening_the_page_scans_and_groups_findings_by_category()
    {
        var chrome = Folder("chrome", ("a", 300));
        var nvidia = Folder("nvidia", ("b", 100), ("c", 100));
        var vm = Create(
            new CleanTarget("chrome-cache", CleanCategory.Browsers, chrome),
            new CleanTarget("nvidia-dx", CleanCategory.Gpu, nvidia));

        await vm.EnsureLoadedAsync();

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal(2, vm.Categories.Count);
        var gpu = vm.Categories.Single(c => c.Category == CleanCategory.Gpu);
        Assert.Equal(200, gpu.Bytes);
        Assert.Equal(500, vm.TotalBytes);
        Assert.False(vm.IsScanning);
        Assert.NotEmpty(vm.Activity);
    }

    [Fact]
    public async Task Safe_total_leaves_out_groups_marked_for_review()
    {
        var vm = Create(
            new CleanTarget("chrome-cache", CleanCategory.Browsers, Folder("s1", ("a", 300))),
            new CleanTarget("claude-chats", CleanCategory.AiTools, Folder("s2", ("b", 100))));

        await vm.EnsureLoadedAsync();

        Assert.Contains(DiskSteward.App.Localization.SizeText.Format(300), vm.SafeText, StringComparison.Ordinal);
        Assert.Contains(DiskSteward.App.Localization.SizeText.Format(100), vm.ReviewText, StringComparison.Ordinal);
        Assert.False(vm.Categories.Single(c => c.Category == CleanCategory.AiTools).IsSafe);
        Assert.True(vm.Categories.Single(c => c.Category == CleanCategory.Browsers).IsSafe);
    }

    [Fact]
    public async Task Details_log_scan_events_not_every_empty_place()
    {
        var vm = Create(
            new CleanTarget("chrome-cache", CleanCategory.Browsers, Folder("e1", ("a", 10))),
            new CleanTarget("yarn-cache", CleanCategory.Developer, Folder("e2")));

        await vm.EnsureLoadedAsync();

        Assert.Equal(2, vm.Activity.Count);
        Assert.All(vm.Activity, e => Assert.False(e.IsProblem));
    }

    [Fact]
    public async Task Categories_start_collapsed_with_their_totals_visible()
    {
        var vm = Create(
            new CleanTarget("chrome-cache", CleanCategory.Browsers, Folder("c1", ("a", 10))),
            new CleanTarget("nvidia-dx", CleanCategory.Gpu, Folder("c2", ("b", 30))));

        await vm.EnsureLoadedAsync();

        Assert.All(vm.Categories, c => Assert.False(c.IsExpanded));
        Assert.All(vm.Categories, c => Assert.True(c.Bytes > 0));
    }

    [Fact]
    public async Task Empty_targets_do_not_show()
    {
        var vm = Create(new CleanTarget("chrome-cache", CleanCategory.Browsers, Folder("empty")));

        await vm.EnsureLoadedAsync();

        Assert.Equal(LoadState.Empty, vm.State);
        Assert.Empty(vm.Categories);
    }

    [Fact]
    public async Task Nothing_is_ticked_after_a_scan()
    {
        var vm = Create(new CleanTarget("chrome-cache", CleanCategory.Browsers, Folder("chrome", ("a", 1))));

        await vm.EnsureLoadedAsync();

        Assert.All(vm.Categories.SelectMany(c => c.Targets), t => Assert.False(t.IsSelected));
        Assert.False(vm.RecycleSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Declining_the_confirmation_recycles_nothing()
    {
        var vm = Create(new CleanTarget("chrome-cache", CleanCategory.Browsers, Folder("chrome", ("a", 1))));
        await vm.EnsureLoadedAsync();
        vm.Categories[0].Targets[0].IsSelected = true;
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(false);

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        Assert.Empty(Binned());
    }

    [Fact]
    public async Task Confirmed_targets_recycle_their_items_and_only_theirs()
    {
        var chrome = Folder("chrome", ("a", 1));
        var nvidia = Folder("nvidia", ("b", 1));
        var vm = Create(
            new CleanTarget("chrome-cache", CleanCategory.Browsers, chrome),
            new CleanTarget("nvidia-dx", CleanCategory.Gpu, nvidia));
        await vm.EnsureLoadedAsync();
        vm.Categories.Single(c => c.Category == CleanCategory.Browsers).Targets[0].IsSelected = true;
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        Assert.Contains(Path.Combine(chrome, "a"), Binned());
        Assert.DoesNotContain(Path.Combine(nvidia, "b"), Binned());
    }

    [Fact]
    public async Task Recycled_places_leave_the_list_without_a_rescan()
    {
        var vm = Create(
            new CleanTarget("chrome-cache", CleanCategory.Browsers, Folder("g1", ("a", 300))),
            new CleanTarget("nvidia-dx", CleanCategory.Gpu, Folder("g2", ("b", 200))));
        await vm.EnsureLoadedAsync();
        vm.Categories.Single(c => c.Category == CleanCategory.Browsers).Targets[0].IsSelected = true;
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        Assert.Equal([CleanCategory.Gpu], vm.Categories.Select(c => c.Category));
        Assert.Equal(200, vm.TotalBytes);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.IsRecycling);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Single(vm.Activity, e => e.Text == DiskSteward.App.Localization.Tr.Get("Clean_LogStarted"));
    }

    [Fact]
    public async Task Selecting_a_category_ticks_its_targets()
    {
        var vm = Create(
            new CleanTarget("chrome-cache", CleanCategory.Browsers, Folder("c1", ("a", 10))),
            new CleanTarget("edge-cache", CleanCategory.Browsers, Folder("c2", ("b", 30))));
        await vm.EnsureLoadedAsync();

        vm.Categories[0].IsSelected = true;

        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal(40, vm.SelectedBytes);
    }
}
