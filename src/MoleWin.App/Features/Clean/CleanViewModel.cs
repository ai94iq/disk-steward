using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using MoleWin.Core.Analyze;
using MoleWin.Core.Clean;
using MoleWin.Core.Safety;

namespace MoleWin.App.Features.Clean;

// Scans every clean rule when the page opens and shows what each holds, grouped by category.
// Nothing is ticked for the user; ticked targets go to the Recycle Bin after a confirmation.
public sealed partial class CleanViewModel : PageViewModel
{
    private const int MaxActivity = 400;
    private const int Parallelism = 4;
    private readonly CleanScanner _scanner;
    private readonly IReadOnlyList<CleanTarget> _targets;
    private readonly SafeRemover _remover;
    private readonly IDialogService _dialogs;
    private readonly ILogger _log;

    public CleanViewModel(CleanScanner scanner, IReadOnlyList<CleanTarget> targets, SafeRemover remover,
        IDialogService dialogs, ILogger<CleanViewModel> log) : base(log)
    {
        _scanner = scanner;
        _targets = targets;
        _remover = remover;
        _dialogs = dialogs;
        _log = log;
        EmptyText = Tr.Get("Clean_Nothing");
    }

    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    [ObservableProperty] public partial bool IsScanning { get; set; }
    [ObservableProperty] public partial string? ScanningText { get; set; }
    [ObservableProperty] public partial string? CurrentFolder { get; set; }
    [ObservableProperty] public partial string? ElapsedText { get; set; }
    [ObservableProperty] public partial string? CountersText { get; set; }
    [ObservableProperty] public partial long TotalBytes { get; set; }
    [ObservableProperty] public partial string? TotalText { get; set; }
    [ObservableProperty] public partial string? FoundText { get; set; }
    [ObservableProperty] public partial string EmptyText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecycleSelectedCommand))]
    public partial int SelectedCount { get; set; }

    [ObservableProperty] public partial long SelectedBytes { get; set; }
    [ObservableProperty] public partial string? SelectedText { get; set; }

    public ObservableCollection<CleanCategoryRow> Categories { get; } = [];

    // Newest first.
    public ObservableCollection<string> Activity { get; } = [];

    protected override async Task<bool> LoadCoreAsync(CancellationToken ct)
    {
        Clear();
        var progress = new ScanProgress();
        var arrived = new ConcurrentQueue<CleanFinding>();
        var clock = Stopwatch.StartNew();
        var current = new ConcurrentDictionary<string, byte>();
        IsScanning = true;
        try
        {
            var work = Task.Run(() => Parallel.ForEach(_targets, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct },
                target =>
                {
                    current[target.Id] = 0;
                    try { arrived.Enqueue(_scanner.Scan(target, progress, DateTime.Now, ct)); }
                    finally { current.TryRemove(target.Id, out _); }
                }), ct);
            while (true)
            {
                var finished = work.IsCompleted;
                Apply(arrived);
                ScanningText = current.Keys.FirstOrDefault() is { } id ? Tr.Format("Clean_Scanning", Label(id)) : null;
                CurrentFolder = progress.CurrentFolder;
                ElapsedText = Tr.Format("Analyze_Elapsed", clock.Elapsed);
                CountersText = Tr.Format("Analyze_Counters", progress.Files, progress.Folders, SizeText.Format(progress.Bytes));
                if (finished) break;
                await Task.Delay(RefreshInterval, ct);
            }
            await work;
            Log(Tr.Format("Clean_LogDone", clock.Elapsed));
        }
        finally
        {
            IsScanning = false;
            ScanningText = null;
            CurrentFolder = null;
        }
        return Categories.Count > 0;
    }

    [RelayCommand]
    private Task RescanAsync() => ReloadCommand.ExecuteAsync(null);

    [RelayCommand(CanExecute = nameof(CanRecycle))]
    private async Task RecycleSelectedAsync()
    {
        var picked = Categories.SelectMany(c => c.Targets).Where(t => t.IsSelected).ToList();
        var items = picked.SelectMany(t => t.Finding.Items).ToList();
        var confirmed = await _dialogs.ConfirmAsync(Tr.Format("Clean_Confirm", items.Count, SizeText.Format(items.Sum(i => i.Size))));
        if (!confirmed) return;

        var results = await Task.Run(() => _remover.Recycle(items.Select(i => i.Path)));
        foreach (var result in results)
        {
            _log.LogInformation("Clean recycle {Path}: {Outcome} {Error}", result.Path, result.Outcome, result.Error);
            Log(result.Outcome switch
            {
                RemovalOutcome.Recycled => Tr.Format("Analyze_LogRecycled", result.Path),
                RemovalOutcome.Protected => Tr.Format("Analyze_LogProtected", result.Path),
                RemovalOutcome.NotRecyclable => Tr.Format("Analyze_LogNotRecyclable", result.Path),
                RemovalOutcome.Missing => Tr.Format("Analyze_LogMissing", result.Path),
                _ => Tr.Format("Analyze_LogFailed", result.Path, result.Error),
            });
        }
        await ReloadCommand.ExecuteAsync(null);
    }

    private bool CanRecycle() => SelectedCount > 0;

    private void Apply(ConcurrentQueue<CleanFinding> arrived)
    {
        var any = false;
        while (arrived.TryDequeue(out var finding))
        {
            var label = Label(finding.Target.Id);
            Log(Tr.Format("Clean_LogChecked", label, SizeText.Format(finding.Size)));
            if (finding.Items.Count == 0) continue;

            var row = new CleanTargetRow(finding, label);
            row.PropertyChanged += OnRowChanged;
            CategoryFor(finding.Target.Category).Add(row);
            TotalBytes += finding.Size;
            any = true;
        }
        if (!any) return;

        TotalText = SizeText.Format(TotalBytes);
        FoundText = Tr.Format("Clean_Found", TotalText, Categories.Sum(c => c.Targets.Count));
        // Bars compare places within their own group; one huge cache elsewhere would flatten them.
        foreach (var category in Categories)
        {
            var largest = category.Targets.Max(t => t.Bytes);
            foreach (var target in category.Targets) target.Share = largest > 0 ? target.Bytes * 100.0 / largest : 0;
        }
    }

    // Categories keep the enum order (System first) whatever order the scan finishes in.
    private CleanCategoryRow CategoryFor(CleanCategory category)
    {
        var existing = Categories.FirstOrDefault(c => c.Category == category);
        if (existing is not null) return existing;
        var row = new CleanCategoryRow(category);
        var at = Categories.TakeWhile(c => c.Category < category).Count();
        Categories.Insert(at, row);
        return row;
    }

    private void Clear()
    {
        foreach (var target in Categories.SelectMany(c => c.Targets)) target.PropertyChanged -= OnRowChanged;
        Categories.Clear();
        TotalBytes = 0;
        TotalText = null;
        FoundText = null;
        UpdateSelection();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanTargetRow.IsSelected)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        var picked = Categories.SelectMany(c => c.Targets).Where(t => t.IsSelected).ToList();
        SelectedCount = picked.Count;
        SelectedBytes = picked.Sum(t => t.Bytes);
        SelectedText = Tr.Format("Clean_Selected", SelectedCount, SizeText.Format(SelectedBytes));
    }

    private void Log(string line)
    {
        Activity.Insert(0, line);
        while (Activity.Count > MaxActivity) Activity.RemoveAt(Activity.Count - 1);
    }

    private static string Label(string id) => Tr.Get($"Clean_Target_{id}");
}
