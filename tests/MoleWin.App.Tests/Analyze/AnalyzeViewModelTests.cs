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
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public AnalyzeViewModelTests()
    {
        _scanner.ListRoots().Returns([new FolderEntry(@"C:\", @"C:\", true, 500L << 30)]);
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

    private void Folder(params (string Path, long Size)[] dirs)
    {
        _scanner.ListChildren(_root).Returns([.. dirs.Select(d => new FolderEntry(Path.GetFileName(d.Path), d.Path, true, null))]);
        foreach (var (path, size) in dirs)
            _scanner.Measure(path, Arg.Any<ScanProgress>(), Arg.Any<CancellationToken>()).Returns(size);
    }

    [Fact]
    public async Task Opening_shows_the_drives_without_measuring_anything()
    {
        var vm = Create();

        await vm.EnsureLoadedAsync();

        Assert.Equal(@"C:\", Assert.Single(vm.Rows).Path);
        _scanner.DidNotReceiveWithAnyArgs().Measure(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_folder_lists_children_then_sorts_them_by_measured_size()
    {
        var small = Child("small");
        var big = Child("big");
        Folder((small, 10), (big, 1000));
        var vm = Create();

        await vm.OpenCommand.ExecuteAsync(_root);

        Assert.Equal(["big", "small"], vm.Rows.Select(r => r.Name));
        Assert.Equal(100, vm.Rows[0].Share);
        Assert.False(vm.IsScanning);
        Assert.NotEmpty(vm.Activity);
    }

    [Fact]
    public async Task Protected_rows_cannot_be_selected()
    {
        var keep = Child("keep");
        Folder((keep, 10));
        var vm = Create(keep);
        await vm.OpenCommand.ExecuteAsync(_root);

        vm.Rows[0].IsSelected = true;

        Assert.False(vm.Rows[0].IsSelected);
        Assert.False(vm.RecycleSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Nothing_is_recycled_when_the_user_declines()
    {
        var cache = Child("cache");
        Folder((cache, 10));
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(false);
        var vm = Create();
        await vm.OpenCommand.ExecuteAsync(_root);
        vm.Rows[0].IsSelected = true;

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        _bin.DidNotReceiveWithAnyArgs().MoveToRecycleBin(default!);
    }

    [Fact]
    public async Task Confirmed_selection_goes_to_the_recycle_bin_and_only_that()
    {
        var cache = Child("cache");
        var keep = Child("other");
        Folder((cache, 10), (keep, 5));
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);
        var vm = Create();
        await vm.OpenCommand.ExecuteAsync(_root);
        vm.Rows.Single(r => r.Name == "cache").IsSelected = true;

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        _bin.Received(1).MoveToRecycleBin(cache);
        _bin.DidNotReceive().MoveToRecycleBin(keep);
    }

    [Fact]
    public async Task Selection_totals_follow_the_ticks()
    {
        var a = Child("a");
        var b = Child("b");
        Folder((a, 10), (b, 30));
        var vm = Create();
        await vm.OpenCommand.ExecuteAsync(_root);

        foreach (var row in vm.Rows) row.IsSelected = true;

        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal(40, vm.SelectedBytes);
        Assert.True(vm.RecycleSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Up_from_a_drive_root_returns_to_the_drive_list()
    {
        _scanner.ListChildren(@"C:\").Returns([]);
        var vm = Create();
        await vm.OpenCommand.ExecuteAsync(@"C:\");

        await vm.UpCommand.ExecuteAsync(null);

        Assert.Null(vm.Location);
        Assert.Equal(@"C:\", Assert.Single(vm.Rows).Path);
    }
}
