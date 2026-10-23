using DiskSteward.App.Features.Apps;
using DiskSteward.App.Services;
using DiskSteward.Core.Analyze;
using DiskSteward.Core.Apps;
using DiskSteward.Core.Safety;
using NSubstitute;

namespace DiskSteward.App.Tests.Apps;

public sealed class AppsViewModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("disksteward-appsvm-").FullName;
    private readonly IInstalledApps _installed = Substitute.For<IInstalledApps>();
    private readonly IUninstallerRunner _runner = Substitute.For<IUninstallerRunner>();
    private readonly IRecycleBin _bin = TestBins.ThatRemoves();

    // Every path handed to the Recycle Bin, across batched calls.
    private string[] Binned() =>
        [.. _bin.ReceivedCalls().SelectMany(c => (IReadOnlyList<string>)c.GetArguments()[0]!)];
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly InstalledApp _tool;
    private readonly InstalledApp _big;

    public AppsViewModelTests()
    {
        _tool = new InstalledApp("k1", "Tool", "Acme", "1.0", 10L << 20, null, "unins.exe", null);
        _big = new InstalledApp("k2", "Big Game", "Studio", "2.0", 5L << 30, null, "unins.exe", null);
        _installed.List().Returns([_tool, _big]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private AppsViewModel Create()
    {
        var rules = new ProtectedPaths(new ProtectedRoots(@"Z:\Windows", [], @"Z:\ProgramData", @"Z:\Users\me", @"Z:\own", [_root]));
        return new AppsViewModel(_installed, _runner, new LeftoverFinder(rules), new LeftoverRoots([_root], []), new FolderScanner(),
            new SafeRemover(rules, _bin, _ => true), _dialogs, Substitute.For<IDateFormatter>(), NullLogger<AppsViewModel>.Instance)
        { PollInterval = TimeSpan.FromMilliseconds(1), PollLimit = TimeSpan.FromMilliseconds(200) };
    }

    [Fact]
    public async Task Sort_by_name_is_a_to_z_and_the_default()
    {
        _installed.List().Returns([_tool, _big, new InstalledApp("k3", "alpha", null, null, 1, null, "u", null)]);
        var vm = Create();

        await vm.EnsureLoadedAsync();

        Assert.Equal(AppSort.Name, vm.SortBy);
        Assert.Equal(["alpha", "Big Game", "Tool"], vm.Apps.Select(a => a.Name));
    }

    [Fact]
    public async Task Sort_by_size_is_largest_first()
    {
        var vm = Create();
        await vm.EnsureLoadedAsync();

        vm.SortBy = AppSort.Size;

        Assert.Equal(["Big Game", "Tool"], vm.Apps.Select(a => a.Name));
    }

    [Fact]
    public async Task Sort_by_install_date_is_newest_first_with_unknown_dates_last()
    {
        _installed.List().Returns([
            _tool with { InstallDate = new DateTime(2024, 1, 1) },
            _big with { InstallDate = null },
            new InstalledApp("k3", "New", null, null, 1, new DateTime(2026, 5, 1), "u", null)]);
        var vm = Create();
        await vm.EnsureLoadedAsync();

        vm.SortBy = AppSort.InstallDate;

        Assert.Equal(["New", "Tool", "Big Game"], vm.Apps.Select(a => a.Name));
    }

    [Fact]
    public async Task Search_filters_by_name_or_publisher()
    {
        var vm = Create();
        await vm.EnsureLoadedAsync();

        vm.SearchText = "acme";

        Assert.Equal("Tool", Assert.Single(vm.Apps).Name);
    }

    [Fact]
    public async Task Declining_the_confirmation_runs_nothing()
    {
        var vm = Create();
        await vm.EnsureLoadedAsync();
        vm.SelectedApp = vm.Apps.Single(a => a.Name == "Tool");
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(false);

        await vm.UninstallCommand.ExecuteAsync(null);

        await _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Leftover_recycle_cannot_start_again_while_its_confirmation_is_open()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Tool"));
        var vm = Create();
        await vm.EnsureLoadedAsync();
        vm.SelectedApp = vm.Apps.Single(a => a.Name == "Tool");
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);
        _installed.IsInstalled(_tool).Returns(false);
        await vm.UninstallCommand.ExecuteAsync(null);
        Assert.Single(vm.Leftovers).IsSelected = true;
        var answer = new TaskCompletionSource<bool>();
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(answer.Task);

        var first = vm.RecycleLeftoversCommand.ExecuteAsync(null);

        Assert.False(vm.RecycleLeftoversCommand.CanExecute(null));
        answer.SetResult(false);
        await first;
    }

    [Fact]
    public async Task The_confirmation_names_the_program_that_will_run()
    {
        // The name comes from the registry entry; the program is what actually starts.
        _installed.List().Returns([_tool with { UninstallString = @"""C:\Tools\Acme\unins000.exe"" /quiet" }]);
        var vm = Create();
        await vm.EnsureLoadedAsync();
        vm.SelectedApp = vm.Apps.Single();
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(false);

        await vm.UninstallCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(Arg.Is<string>(s => s.Contains(@"C:\Tools\Acme\unins000.exe") && s.Contains("Tool")));
    }

    [Fact]
    public async Task After_uninstall_leftovers_are_listed_unticked()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Tool"));
        File.WriteAllBytes(Path.Combine(_root, "Tool", "settings.json"), new byte[42]);
        var vm = Create();
        await vm.EnsureLoadedAsync();
        vm.SelectedApp = vm.Apps.Single(a => a.Name == "Tool");
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);
        _installed.IsInstalled(_tool).Returns(false);

        await vm.UninstallCommand.ExecuteAsync(null);

        await _runner.Received(1).RunAsync(_tool, Arg.Any<CancellationToken>());
        var leftover = Assert.Single(vm.Leftovers);
        Assert.Equal(42, leftover.Size);
        Assert.False(leftover.IsSelected);
        Assert.Empty(Binned());
    }

    [Fact]
    public async Task An_app_still_installed_gets_no_leftover_scan()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Tool"));
        var vm = Create();
        await vm.EnsureLoadedAsync();
        vm.SelectedApp = vm.Apps.Single(a => a.Name == "Tool");
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);
        _installed.IsInstalled(_tool).Returns(true);

        await vm.UninstallCommand.ExecuteAsync(null);

        Assert.Empty(vm.Leftovers);
    }

    [Fact]
    public async Task Ticked_leftovers_go_to_the_recycle_bin_after_confirmation()
    {
        var folder = Path.Combine(_root, "Tool");
        Directory.CreateDirectory(folder);
        var vm = Create();
        await vm.EnsureLoadedAsync();
        vm.SelectedApp = vm.Apps.Single(a => a.Name == "Tool");
        _dialogs.ConfirmAsync(Arg.Any<string>()).Returns(true);
        _installed.IsInstalled(_tool).Returns(false);
        await vm.UninstallCommand.ExecuteAsync(null);

        vm.Leftovers[0].IsSelected = true;
        await vm.RecycleLeftoversCommand.ExecuteAsync(null);

        Assert.Contains(folder, Binned());
    }
}
