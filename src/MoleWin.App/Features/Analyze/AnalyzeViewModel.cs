using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using MoleWin.Core.Analyze;
using MoleWin.Core.Safety;

namespace MoleWin.App.Features.Analyze;

// Browse folders by size, tick what to remove, confirm, and it goes to the Recycle Bin.
// Opening a folder lists it at once; folder sizes then arrive as background measuring
// finishes. Nothing is ever removed without the tick and the confirmation.
public sealed partial class AnalyzeViewModel : PageViewModel
{
    private const int MaxActivity = 400;
    private const int Parallelism = 4;
    private readonly IFolderScanner _scanner;
    private readonly ProtectedPaths _rules;
    private readonly SafeRemover _remover;
    private readonly IDialogService _dialogs;
    private readonly ILogger _log;
    private CancellationTokenSource? _scan;

    public AnalyzeViewModel(IFolderScanner scanner, ProtectedPaths rules, SafeRemover remover, IDialogService dialogs,
        ILogger<AnalyzeViewModel> log) : base(log)
    {
        _scanner = scanner;
        _rules = rules;
        _remover = remover;
        _dialogs = dialogs;
        _log = log;
    }

    // How often counters and arriving sizes are pushed to the screen.
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    // Null is the drive list.
    [ObservableProperty] public partial string? Location { get; set; }
    [ObservableProperty] public partial bool IsScanning { get; set; }
    [ObservableProperty] public partial string? CurrentFolder { get; set; }
    [ObservableProperty] public partial string? ElapsedText { get; set; }
    [ObservableProperty] public partial string? CountersText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecycleSelectedCommand))]
    public partial int SelectedCount { get; set; }

    [ObservableProperty] public partial long SelectedBytes { get; set; }
    [ObservableProperty] public partial string? SelectedText { get; set; }

    public ObservableCollection<AnalyzeRow> Rows { get; } = [];

    // Newest first.
    public ObservableCollection<string> Activity { get; } = [];

    protected override async Task<bool> LoadCoreAsync(CancellationToken ct)
    {
        await ShowAsync(Location);
        return true;
    }

    [RelayCommand]
    private Task OpenAsync(string? path) =>
        path is null || Directory.Exists(path) ? ShowAsync(path) : Task.CompletedTask;

    [RelayCommand]
    private Task UpAsync()
    {
        if (Location is null) return Task.CompletedTask;
        // The parent of a drive root is null: the drive list.
        return ShowAsync(Path.GetDirectoryName(Location));
    }

    [RelayCommand]
    private void Stop() => _scan?.Cancel();

    [RelayCommand(CanExecute = nameof(CanRecycle))]
    private async Task RecycleSelectedAsync()
    {
        var picked = Rows.Where(r => r.IsSelected).ToList();
        var confirmed = await _dialogs.ConfirmAsync(Tr.Format("Analyze_Confirm", picked.Count, SizeText.Format(picked.Sum(r => r.Size))));
        if (!confirmed) return;

        var results = await Task.Run(() => _remover.Recycle(picked.Select(r => r.Path)));
        foreach (var result in results)
        {
            _log.LogInformation("Recycle {Path}: {Outcome} {Error}", result.Path, result.Outcome, result.Error);
            Log(result.Outcome switch
            {
                RemovalOutcome.Recycled => Tr.Format("Analyze_LogRecycled", result.Path),
                RemovalOutcome.Protected => Tr.Format("Analyze_LogProtected", result.Path),
                RemovalOutcome.NotRecyclable => Tr.Format("Analyze_LogNotRecyclable", result.Path),
                RemovalOutcome.Missing => Tr.Format("Analyze_LogMissing", result.Path),
                _ => Tr.Format("Analyze_LogFailed", result.Path, result.Error),
            });
        }
        await ShowAsync(Location);
    }

    private bool CanRecycle() => SelectedCount > 0;

    private async Task ShowAsync(string? path)
    {
        // A new folder supersedes the scan in progress. The sources hold no timers, so they
        // are left to the GC rather than disposed while a cancelled scan may still read them.
        _scan?.Cancel();
        _scan = new CancellationTokenSource();
        var ct = _scan.Token;
        Location = path;
        ClearRows();

        var entries = await Task.Run(() => path is null ? _scanner.ListRoots() : _scanner.ListChildren(path));
        if (ct.IsCancellationRequested) return;
        foreach (var entry in entries)
        {
            var row = new AnalyzeRow(entry.Name, entry.Path, entry.IsDirectory, _rules.IsProtected(entry.Path));
            row.PropertyChanged += OnRowChanged;
            if (entry.Size is { } size) SetSize(row, size);
            else row.IsMeasuring = true;
            Rows.Add(row);
        }
        Resort();
        if (path is not null) await MeasureAsync(path, ct);
    }

    // Folders are measured in parallel off the UI thread; their sizes and the counters are
    // applied here, on one thread, every RefreshInterval.
    private async Task MeasureAsync(string path, CancellationToken ct)
    {
        var pending = Rows.Where(r => r.IsMeasuring).ToList();
        var progress = new ScanProgress();
        var arrived = new ConcurrentQueue<(AnalyzeRow Row, long Size)>();
        var clock = Stopwatch.StartNew();
        IsScanning = true;
        Log(Tr.Format("Analyze_LogScan", path));

        var work = Task.Run(() => Parallel.ForEach(pending, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct },
            row => arrived.Enqueue((row, _scanner.Measure(row.Path, progress, ct)))), ct);
        try
        {
            while (true)
            {
                var finished = work.IsCompleted;
                Apply(arrived);
                Report(progress, clock.Elapsed);
                if (finished) break;
                await Task.Delay(RefreshInterval, ct);
            }
            await work;
            Log(Tr.Format("Analyze_LogDone", path, clock.Elapsed, progress.Files));
        }
        catch (OperationCanceledException)
        {
            Log(Tr.Format("Analyze_LogStopped", path));
        }
        finally
        {
            IsScanning = false;
            CurrentFolder = null;
            foreach (var row in pending) row.IsMeasuring = false;
        }
    }

    private void Apply(ConcurrentQueue<(AnalyzeRow Row, long Size)> arrived)
    {
        var any = false;
        while (arrived.TryDequeue(out var item))
        {
            item.Row.IsMeasuring = false;
            SetSize(item.Row, item.Size);
            Log(Tr.Format("Analyze_LogMeasured", item.Row.Name, item.Row.SizeText));
            any = true;
        }
        if (any) Resort();
    }

    private void Report(ScanProgress progress, TimeSpan elapsed)
    {
        CurrentFolder = progress.CurrentFolder;
        ElapsedText = Tr.Format("Analyze_Elapsed", elapsed);
        CountersText = Tr.Format("Analyze_Counters", progress.Files, progress.Folders, SizeText.Format(progress.Bytes));
    }

    private static void SetSize(AnalyzeRow row, long size)
    {
        row.Size = size;
        row.SizeText = SizeText.Format(size);
    }

    // Largest first, and bars relative to the largest.
    private void Resort()
    {
        var sorted = Rows.OrderByDescending(r => r.Size).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var at = Rows.IndexOf(sorted[i]);
            if (at != i) Rows.Move(at, i);
        }
        var largest = Rows.Count > 0 ? Rows.Max(r => r.Size) : 0;
        foreach (var row in Rows) row.Share = largest > 0 ? row.Size * 100.0 / largest : 0;
    }

    private void ClearRows()
    {
        foreach (var row in Rows) row.PropertyChanged -= OnRowChanged;
        Rows.Clear();
        UpdateSelection();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AnalyzeRow.IsSelected) or nameof(AnalyzeRow.Size)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        var picked = Rows.Where(r => r.IsSelected).ToList();
        SelectedCount = picked.Count;
        SelectedBytes = picked.Sum(r => r.Size);
        SelectedText = Tr.Format("Analyze_Selected", SelectedCount, SizeText.Format(SelectedBytes));
    }

    private void Log(string line)
    {
        Activity.Insert(0, line);
        while (Activity.Count > MaxActivity) Activity.RemoveAt(Activity.Count - 1);
    }
}
