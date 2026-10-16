using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using MoleWin.Core.Analyze;
using MoleWin.Core.Safety;

namespace MoleWin.App.Features.Analyze;

// A tree of drives and folders by size. Expanding a node lists it at once; folder sizes then
// arrive from background measuring. Ticked items go to the Recycle Bin after a confirmation;
// drives, protected folders and system files can never be ticked.
public sealed partial class AnalyzeViewModel : PageViewModel
{
    private const int Parallelism = 4;
    private readonly IFolderScanner _scanner;
    private readonly ProtectedPaths _rules;
    private readonly SafeRemover _remover;
    private readonly IDialogService _dialogs;
    private readonly ILogger _log;
    private readonly HashSet<FolderNode> _selected = [];
    private CancellationTokenSource _stop = new();
    private int _measuring;

    public AnalyzeViewModel(IFolderScanner scanner, ProtectedPaths rules, SafeRemover remover, IDialogService dialogs,
        ILogger<AnalyzeViewModel> log) : base(log)
    {
        _scanner = scanner;
        _rules = rules;
        _remover = remover;
        _dialogs = dialogs;
        _log = log;
        SelectedText = Tr.Format("Analyze_Selected", 0, SizeText.Format(0));
    }

    // How often counters and arriving sizes are pushed to the screen.
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    [ObservableProperty] public partial bool IsScanning { get; set; }
    [ObservableProperty] public partial string? CurrentFolder { get; set; }
    [ObservableProperty] public partial string? ElapsedText { get; set; }
    [ObservableProperty] public partial string? CountersText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecycleSelectedCommand))]
    public partial int SelectedCount { get; set; }

    [ObservableProperty] public partial long SelectedBytes { get; set; }
    [ObservableProperty] public partial string? SelectedText { get; set; }

    public ObservableCollection<FolderNode> Roots { get; } = [];

    public ActivityLog Activity { get; } = [];

    protected override async Task<bool> LoadCoreAsync(CancellationToken ct)
    {
        var drives = await Task.Run(_scanner.ListRoots, ct);
        Roots.Clear();
        foreach (var drive in drives) Roots.Add(new FolderNode(drive, FolderKind.Drive, null, _rules.ReasonFor(drive.Path), ExpandAsync));
        return Roots.Count > 0;
    }

    // Lists a drive or folder the first time it opens, then measures its subfolders.
    public async Task ExpandAsync(FolderNode node)
    {
        if (node.IsLoaded) return;
        node.IsLoaded = true;
        var entries = await Task.Run(() => _scanner.ListChildren(node.Path));
        node.Children.Clear();
        foreach (var entry in entries)
        {
            var child = new FolderNode(entry, entry.IsDirectory ? FolderKind.Folder : FolderKind.File, node,
                _rules.ReasonFor(entry.Path), ExpandAsync);
            child.PropertyChanged += OnNodeChanged;
            if (entry.IsDirectory) child.IsMeasuring = true;
            node.Children.Add(child);
        }
        Resort(node);
        await MeasureAsync(node);
    }

    [RelayCommand]
    private void Stop()
    {
        _stop.Cancel();
        _stop = new CancellationTokenSource();
    }

    [RelayCommand(CanExecute = nameof(CanRecycle))]
    private async Task RecycleSelectedAsync()
    {
        var picked = TopSelected();
        var confirmed = await _dialogs.ConfirmAsync(Tr.Format("Analyze_Confirm", picked.Count, SizeText.Format(picked.Sum(n => n.Size))));
        if (!confirmed) return;

        var results = await Task.Run(() => _remover.Recycle(picked.Select(n => n.Path)));
        foreach (var result in results)
            _log.LogInformation("Recycle {Path}: {Outcome} {Error}", result.Path, result.Outcome, result.Error);
        Activity.AddRemovals(results);

        foreach (var node in picked) Forget(node);
        foreach (var parent in picked.Select(n => n.Parent).OfType<FolderNode>().Distinct())
        {
            parent.IsLoaded = false;
            await ExpandAsync(parent);
        }
    }

    private bool CanRecycle() => SelectedCount > 0;

    // Folders are measured in parallel off the UI thread; sizes and counters are applied
    // here, on one thread, every RefreshInterval.
    private async Task MeasureAsync(FolderNode node)
    {
        var pending = node.Children.Where(c => c.IsMeasuring).ToList();
        if (pending.Count == 0) return;
        var ct = _stop.Token;
        var progress = new ScanProgress();
        var arrived = new ConcurrentQueue<(FolderNode Node, long Size)>();
        var clock = Stopwatch.StartNew();
        _measuring++;
        IsScanning = true;
        Log(Tr.Format("Analyze_LogScan", node.Path));

        var work = Task.Run(() => Parallel.ForEach(pending, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct },
            child => arrived.Enqueue((child, _scanner.Measure(child.Path, progress, ct)))), ct);
        try
        {
            while (true)
            {
                var finished = work.IsCompleted;
                Apply(node, arrived);
                CurrentFolder = progress.CurrentFolder;
                ElapsedText = Tr.Format("Analyze_Elapsed", clock.Elapsed);
                CountersText = Tr.Format("Analyze_Counters", progress.Files, progress.Folders, SizeText.Format(progress.Bytes));
                if (finished) break;
                await Task.Delay(RefreshInterval, ct);
            }
            await work;
            Log(Tr.Format("Analyze_LogDone", node.Path, clock.Elapsed, progress.Files));
        }
        catch (OperationCanceledException)
        {
            Log(Tr.Format("Analyze_LogStopped", node.Path));
        }
        finally
        {
            foreach (var child in pending) child.IsMeasuring = false;
            if (--_measuring == 0)
            {
                IsScanning = false;
                CurrentFolder = null;
            }
        }
    }

    private void Apply(FolderNode node, ConcurrentQueue<(FolderNode Node, long Size)> arrived)
    {
        var any = false;
        while (arrived.TryDequeue(out var item))
        {
            item.Node.IsMeasuring = false;
            item.Node.SetSize(item.Size);
            any = true;
        }
        if (any) Resort(node);
    }

    // Largest first, and bars relative to the largest sibling.
    private static void Resort(FolderNode node)
    {
        var children = node.Children;
        var sorted = children.OrderByDescending(c => c.Size).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var at = children.IndexOf(sorted[i]);
            if (at != i) children.Move(at, i);
        }
        var largest = children.Count > 0 ? children.Max(c => c.Size) : 0;
        foreach (var child in children) child.Share = largest > 0 ? child.Size * 100.0 / largest : 0;
    }

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FolderNode node || e.PropertyName is not (nameof(FolderNode.IsSelected) or nameof(FolderNode.Size))) return;
        if (node.IsSelected) _selected.Add(node);
        else _selected.Remove(node);
        UpdateSelection();
    }

    private void Forget(FolderNode node)
    {
        node.PropertyChanged -= OnNodeChanged;
        _selected.Remove(node);
        foreach (var child in node.Children) Forget(child);
        UpdateSelection();
    }

    // A ticked folder covers anything ticked inside it.
    private List<FolderNode> TopSelected() =>
        [.. _selected.Where(n => !Ancestors(n).Any(_selected.Contains))];

    private static IEnumerable<FolderNode> Ancestors(FolderNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent) yield return parent;
    }

    private void UpdateSelection()
    {
        var picked = TopSelected();
        SelectedCount = picked.Count;
        SelectedBytes = picked.Sum(n => n.Size);
        SelectedText = Tr.Format("Analyze_Selected", SelectedCount, SizeText.Format(SelectedBytes));
    }

    private void Log(string line) => Activity.Add(line);
}
