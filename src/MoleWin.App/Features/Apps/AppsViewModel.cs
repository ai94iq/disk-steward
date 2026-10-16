using System.Collections.ObjectModel;
using MoleWin.Core.Analyze;
using MoleWin.Core.Apps;
using MoleWin.Core.Safety;

namespace MoleWin.App.Features.Apps;

// Pick an app, confirm, and its own uninstaller runs. Once Windows no longer lists the app,
// exact-name leftovers are shown unticked; ticked ones go to the Recycle Bin after a second
// confirmation.
public sealed partial class AppsViewModel : PageViewModel
{
    private const int MaxActivity = 200;
    private readonly IInstalledApps _installed;
    private readonly IUninstallerRunner _runner;
    private readonly LeftoverFinder _finder;
    private readonly LeftoverRoots _roots;
    private readonly IFolderScanner _folders;
    private readonly SafeRemover _remover;
    private readonly IDialogService _dialogs;
    private readonly ILogger _log;
    private List<AppRow> _all = [];
    private CancellationTokenSource? _wait;

    public AppsViewModel(IInstalledApps installed, IUninstallerRunner runner, LeftoverFinder finder, LeftoverRoots roots,
        IFolderScanner folders, SafeRemover remover, IDialogService dialogs, ILogger<AppsViewModel> log) : base(log)
    {
        _installed = installed;
        _runner = runner;
        _finder = finder;
        _roots = roots;
        _folders = folders;
        _remover = remover;
        _dialogs = dialogs;
        _log = log;
        SearchText = "";
    }

    // Many uninstallers hand off to a copy of themselves and exit at once, so the entry is
    // watched until it disappears, up to PollLimit.
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan PollLimit { get; init; } = TimeSpan.FromMinutes(10);

    [ObservableProperty] public partial string SearchText { get; set; }
    [ObservableProperty] public partial string? CountText { get; set; }
    [ObservableProperty] public partial string? StatusText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    public partial AppRow? SelectedApp { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecycleLeftoversCommand))]
    public partial int SelectedLeftovers { get; set; }

    public ObservableCollection<AppRow> Apps { get; } = [];
    public ObservableCollection<LeftoverRow> Leftovers { get; } = [];

    // Newest first.
    public ObservableCollection<string> Activity { get; } = [];

    partial void OnSearchTextChanged(string value) => Filter();

    protected override async Task<bool> LoadCoreAsync(CancellationToken ct)
    {
        var apps = await Task.Run(_installed.List, ct);
        _all = [.. apps.OrderByDescending(a => a.SizeBytes).Select(a => new AppRow(a))];
        Filter();
        return _all.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallAsync()
    {
        if (SelectedApp is not { } row) return;
        if (!await _dialogs.ConfirmAsync(Tr.Format("Apps_Confirm", row.Name))) return;

        IsBusy = true;
        ClearLeftovers();
        _wait = new CancellationTokenSource();
        try
        {
            Log(Tr.Format("Apps_LogRunning", row.Name));
            StatusText = Tr.Format("Apps_Running", row.Name);
            await _runner.RunAsync(row.App, _wait.Token);
            if (!await WaitUntilGoneAsync(row.App, _wait.Token))
            {
                StatusText = Tr.Format("Apps_StillInstalled", row.Name);
                Log(StatusText);
                return;
            }
            Log(Tr.Format("Apps_LogRemoved", row.Name));
            await FindLeftoversAsync(row.App);
            await ReloadCommand.ExecuteAsync(null);
        }
        catch (OperationCanceledException)
        {
            StatusText = Tr.Format("Apps_StillInstalled", row.Name);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // The user declined the admin prompt, or the uninstaller is missing.
            _log.LogWarning(ex, "Uninstaller for {App} did not run", row.Name);
            StatusText = Tr.Format("Apps_RunFailed", row.Name);
            Log(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUninstall() => SelectedApp is not null && !IsBusy;

    [RelayCommand]
    private void StopWaiting() => _wait?.Cancel();

    [RelayCommand(CanExecute = nameof(CanRecycleLeftovers))]
    private async Task RecycleLeftoversAsync()
    {
        var picked = Leftovers.Where(l => l.IsSelected).ToList();
        if (!await _dialogs.ConfirmAsync(Tr.Format("Analyze_Confirm", picked.Count, SizeText.Format(picked.Sum(l => l.Size))))) return;

        foreach (var result in await Task.Run(() => _remover.Recycle(picked.Select(l => l.Path))))
        {
            _log.LogInformation("Leftover recycle {Path}: {Outcome} {Error}", result.Path, result.Outcome, result.Error);
            Log(result.Outcome == RemovalOutcome.Recycled
                ? Tr.Format("Analyze_LogRecycled", result.Path)
                : Tr.Format("Analyze_LogFailed", result.Path, result.Outcome));
        }
        ClearLeftovers();
        StatusText = null;
    }

    private bool CanRecycleLeftovers() => SelectedLeftovers > 0;

    private async Task<bool> WaitUntilGoneAsync(InstalledApp app, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + PollLimit;
        while (_installed.IsInstalled(app))
        {
            if (DateTime.UtcNow >= deadline) return false;
            StatusText = Tr.Format("Apps_Waiting", app.Name);
            await Task.Delay(PollInterval, ct);
        }
        return true;
    }

    private async Task FindLeftoversAsync(InstalledApp app)
    {
        var found = await Task.Run(() => _finder.Find(app, _roots)
            .Select(path => new LeftoverRow(path, Directory.Exists(path) ? _folders.Measure(path, new ScanProgress(), CancellationToken.None) : new FileInfo(path).Length))
            .ToList());
        foreach (var row in found)
        {
            row.PropertyChanged += (_, _) => SelectedLeftovers = Leftovers.Count(l => l.IsSelected);
            Leftovers.Add(row);
            Log(Tr.Format("Apps_LogLeftover", row.Path, row.SizeText));
        }
        StatusText = found.Count > 0 ? Tr.Format("Apps_LeftoversFound", app.Name, found.Count) : Tr.Format("Apps_Clean", app.Name);
    }

    private void ClearLeftovers()
    {
        Leftovers.Clear();
        SelectedLeftovers = 0;
    }

    private void Filter()
    {
        var term = SearchText.Trim();
        var shown = term.Length == 0
            ? _all
            : _all.Where(a => a.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || (a.App.Publisher?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)).ToList();
        Apps.Clear();
        foreach (var app in shown) Apps.Add(app);
        CountText = Tr.Format("Apps_Count", shown.Count, _all.Count);
    }

    private void Log(string line)
    {
        Activity.Insert(0, line);
        while (Activity.Count > MaxActivity) Activity.RemoveAt(Activity.Count - 1);
    }
}
