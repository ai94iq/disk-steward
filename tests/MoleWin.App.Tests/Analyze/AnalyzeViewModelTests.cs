using MoleWin.App.Features.Analyze;
using MoleWin.App.Services;
using MoleWin.Core.Analyze;
using MoleWin.Core.Safety;
using NSubstitute;

namespace MoleWin.App.Tests.Analyze;

public sealed class AnalyzeViewModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("molewin-analyze-").FullName;
    private readonly IFolderScanner _scanner = Substitute.For<IFolderScanner>();
    private readonly IRecycleBin _bin = Substitute.For<IRecycleBin>();

    // Every path handed to the Recycle Bin, across batched calls.
    private string[] Binned() =>
        [.. _bin.ReceivedCalls().SelectMany(c => (IReadOnlyList<string>)c.GetArguments()[0]!)];
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public AnalyzeViewModelTests()
    {
        // One "drive": the temp folder, 400 of 1000 bytes used.
        _scanner.ListRoots().Returns([new FolderEntry("T:", _root, true, 400, 1000)]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Child(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private AnalyzeViewModel Create(params string[] protectedFolders)
    {
        var rules = new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [], @"Z:\ProgramData", @"Z:\Users\me", @"Z:\own", protectedFolders));
        return new AnalyzeViewModel(_scanner, rules, new SafeRemover(rules, _bin, _ => true), _dialogs,
            NullLogger<AnalyzeViewModel>.Instance) { RefreshInterval = TimeSpan.FromMilliseconds(1) };
    }

    private void Children(params FolderEntry[] entries)
    {
        _scanner.ListChildren(_root).Returns(entries);
        foreach (var e in entries.Where(e => e.IsDirectory))
            _scanner.Measure(e.Path, Arg.Any<ScanProgress>(), Arg.Any<CancellationToken>()).Returns(e.Size ?? 0);
    }

    private static FolderEntry Dir(string path, long measured) => new(Path.GetFileName(path), path, true, measured);

    private async Task<FolderNode> OpenDrive(AnalyzeViewModel vm)
    {
        await vm.EnsureLoadedAsync();
        var drive = Assert.Single(vm.Roots);
        await vm.ExpandAsync(drive);
        return drive;
    }

    [Fact]
    public async Task Drives_have_no_tick_box_and_show_used_share_of_capacity()
    {
        var vm = Create();

        await vm.EnsureLoadedAsync();

        var drive = Assert.Single(vm.Roots);
        Assert.Equal(FolderKind.Drive, drive.Kind);
        Assert.False(drive.CanSelect);
        Assert.Equal(40, drive.UsedPercent);
        _scanner.DidNotReceiveWithAnyArgs().Measure(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Expanding_a_folder_lists_children_once_and_measures_them_largest_first()
    {
        Children(Dir(Child("small"), 10), Dir(Child("big"), 1000));
        var vm = Create();
        var drive = await OpenDrive(vm);

        await vm.ExpandAsync(drive);

        _scanner.Received(1).ListChildren(_root);
        Assert.Equal(["big", "small"], drive.Children.Select(c => c.Name));
        Assert.Equal(100, drive.Children[0].Share);
        Assert.False(vm.IsScanning);
    }

    [Fact]
    public async Task System_items_cannot_be_ticked()
    {
        Children(new FolderEntry("pagefile.sys", Path.Combine(_root, "pagefile.sys"), false, 5, IsSystem: true));
        var vm = Create();
        var drive = await OpenDrive(vm);

        var file = Assert.Single(drive.Children);
        file.IsSelected = true;

        Assert.False(file.CanSelect);
        Assert.False(file.IsSelected);
    }

    [Fact]
    public async Task Protected_folders_cannot_be_ticked()
    {
        var keep = Child("keep");
        Children(Dir(keep, 10));
        var vm = Create(keep);
        var drive = await OpenDrive(vm);

        drive.Children[0].IsSelected = true;

        Assert.False(drive.Children[0].IsSelected);
        Assert.False(vm.RecycleSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Ticks_anywhere_in_the_tree_add_up()
    {
        Children(Dir(Child("a"), 10), Dir(Child("b"), 30));
        var vm = Create();
        var drive = await OpenDrive(vm);

        foreach (var node in drive.Children) node.IsSelected = true;

        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal(40, vm.SelectedBytes);
    }

    [Fact]
    public async Task Nothing_is_recycled_when_the_user_declines()
    {
        Children(Dir(Child("cache"), 10));
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(false);
        var vm = Create();
        var drive = await OpenDrive(vm);
        drive.Children[0].IsSelected = true;

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        Assert.Empty(Binned());
    }

    [Fact]
    public async Task Confirmed_selection_goes_to_the_recycle_bin_and_only_that()
    {
        var cache = Child("cache");
        var other = Child("other");
        Children(Dir(cache, 10), Dir(other, 5));
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);
        var vm = Create();
        var drive = await OpenDrive(vm);
        drive.Children.Single(c => c.Name == "cache").IsSelected = true;

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        Assert.Contains(cache, Binned());
        Assert.DoesNotContain(other, Binned());
    }

    [Fact]
    public async Task Recycled_folder_leaves_the_tree_and_its_size_comes_off_without_a_rescan()
    {
        var cache = Child("cache");
        var other = Child("other");
        Children(Dir(cache, 10), Dir(other, 5));
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);
        var vm = Create();
        var drive = await OpenDrive(vm);
        var before = drive.Size;
        drive.Children.Single(c => c.Name == "cache").IsSelected = true;

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        Assert.Equal(["other"], drive.Children.Select(c => c.Name));
        Assert.Equal(Math.Max(0, before - 10), drive.Size);
        Assert.Equal(0, vm.SelectedCount);
        _scanner.Received(1).ListChildren(_root);
    }
}
