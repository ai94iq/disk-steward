using System.Collections.ObjectModel;
using DiskSteward.Core.Monitor;

namespace DiskSteward.App.Features.Monitor;

// Read-only dashboard: one sample for first paint, then a sample every two seconds while
// the page is on screen.
public sealed partial class MonitorViewModel : PageViewModel
{
    // 30 samples at two seconds: the last minute.
    public const int HistoryLength = 30;
    private const double Gb = 1L << 30;
    private readonly IMonitorService _monitor;
    private readonly IKeepAwake _awake;
    private readonly ILogger _log;
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _memoryHistory = new();
    private readonly Queue<double> _downHistory = new();
    private CancellationTokenSource? _live;
    private bool _showingEnd;

    public MonitorViewModel(IMonitorService monitor, IKeepAwake awake, ILogger<MonitorViewModel> log) : base(log)
    {
        _monitor = monitor;
        _awake = awake;
        KeepAwake = KeepAwakeOption.All[0];
        // Both are singletons, so this subscription lives as long as the app. The service has
        // already stopped, so only the shown option changes.
        _awake.Ended += (_, _) =>
        {
            _showingEnd = true;
            try { KeepAwake = KeepAwakeOption.All[0]; }
            finally { _showingEnd = false; }
        };
        _log = log;
        CpuHistory = [];
        MemoryHistory = [];
        DownHistory = [];
    }

    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(2);

    [ObservableProperty] public partial int HealthScore { get; set; }
    [ObservableProperty] public partial string? HealthMessage { get; set; }
    [ObservableProperty] public partial LoadLevel HealthLevel { get; set; }
    [ObservableProperty] public partial string? Device { get; set; }
    [ObservableProperty] public partial string? DeviceDetail { get; set; }
    [ObservableProperty] public partial string? UptimeText { get; set; }
    [ObservableProperty] public partial string? CpuPercentText { get; set; }
    [ObservableProperty] public partial LoadLevel CpuLevel { get; set; }
    [ObservableProperty] public partial string? CpuDetail { get; set; }
    [ObservableProperty] public partial IReadOnlyList<double> CpuHistory { get; set; }
    [ObservableProperty] public partial string? MemoryPercentText { get; set; }
    [ObservableProperty] public partial LoadLevel MemoryLevel { get; set; }
    [ObservableProperty] public partial string? MemoryText { get; set; }
    [ObservableProperty] public partial IReadOnlyList<double> MemoryHistory { get; set; }
    [ObservableProperty] public partial string? NetworkText { get; set; }
    [ObservableProperty] public partial string? NetworkDetail { get; set; }
    [ObservableProperty] public partial IReadOnlyList<double> DownHistory { get; set; }

    // Bumps once per sample; the view pulses the live dot on each change.
    [ObservableProperty] public partial int Pulse { get; set; }
    [ObservableProperty] public partial bool IsLive { get; set; }

    public IReadOnlyList<KeepAwakeOption> KeepAwakeOptions => KeepAwakeOption.All;

    // Stops the PC and display from sleeping; the app keeps one setting for all pages.
    [ObservableProperty] public partial KeepAwakeOption KeepAwake { get; set; }

    partial void OnKeepAwakeChanged(KeepAwakeOption oldValue, KeepAwakeOption newValue)
    {
        // The constructor's first assignment (from no value) is not a user choice, and neither is
        // a timed request running out.
        if (oldValue is null || _showingEnd) return;
        if (newValue.IsOn) _awake.Start(newValue.Duration);
        else _awake.Stop();
    }

    public ObservableCollection<DriveRow> Drives { get; } = [];
    public ObservableCollection<ProcessRow> Processes { get; } = [];

    protected override async Task<bool> LoadCoreAsync(CancellationToken ct)
    {
        Apply(await Task.Run(_monitor.Sample, ct));
        return true;
    }

    // Called by the page while it is on screen.
    public void StartLive()
    {
        StopLive();
        _live = new CancellationTokenSource();
        _ = RunLiveAsync(_live.Token);
    }

    public void StopLive()
    {
        _live?.Cancel();
        _live = null;
    }

    // Samples until cancelled; a failed sample is logged and the next one tried.
    public async Task RunLiveAsync(CancellationToken ct, int? samples = null)
    {
        IsLive = true;
        try
        {
            for (var i = 0; samples is null || i < samples; i++)
            {
                await Task.Delay(Interval, ct);
                try
                {
                    Apply(await Task.Run(_monitor.Sample, ct));
                    MarkQuietLoadCompleted(true);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogWarning(ex, "Monitor sample failed");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            IsLive = false;
        }
    }

    private void Apply(SystemSnapshot s)
    {
        HealthScore = s.Health.Score;
        HealthLevel = LoadLevels.Health(s.Health.Score);
        HealthMessage = Tr.Get($"Monitor_Issue_{s.Health.Issue}");
        Device = s.Device.Model;
        DeviceDetail = Tr.Format("Monitor_DeviceDetail", s.Device.OsName, s.MemoryTotal / Gb);
        if (s.Battery is { } b) DeviceDetail = Tr.Format("Monitor_WithBattery", DeviceDetail, b.Percent);
        UptimeText = Tr.Format(s.Uptime.Days > 0 ? "Monitor_UptimeDays" : "Monitor_Uptime", s.Uptime, s.ProcessCount);

        CpuPercentText = Tr.Format("Monitor_Percent", s.CpuPercent);
        CpuLevel = LoadLevels.Cpu(s.CpuPercent);
        CpuDetail = Tr.Format("Monitor_CpuDetail", s.Device.CpuModel, s.Device.LogicalProcessors);
        CpuHistory = Push(_cpuHistory, s.CpuPercent);

        MemoryPercentText = Tr.Format("Monitor_Percent", s.MemoryPercent);
        MemoryLevel = LoadLevels.Fill(s.MemoryPercent);
        MemoryText = Tr.Format("Monitor_MemoryValue", s.MemoryUsed / Gb, s.MemoryTotal / Gb);
        MemoryHistory = Push(_memoryHistory, s.MemoryPercent);

        NetworkText = Tr.Format("Monitor_Rate", SizeText.Format((long)s.NetworkDownBytesPerSecond));
        NetworkDetail = Tr.Format("Monitor_Upload", SizeText.Format((long)s.NetworkUpBytesPerSecond));
        DownHistory = Push(_downHistory, s.NetworkDownBytesPerSecond);

        Replace(Drives, s.Drives.Select(d => new DriveRow(
            d.Root, d.UsedPercent, LoadLevels.Fill(d.UsedPercent),
            Tr.Format("Monitor_DriveFree", (d.Total - d.Used) / Gb, d.Total / Gb),
            d.Removable ? Tr.Format("Monitor_DriveRemovable", d.Format) : d.Format)));
        Replace(Processes, s.TopProcesses.Select(p => new ProcessRow(
            p.Count > 1 ? Tr.Format("Monitor_ProcessGroup", p.Name, p.Count) : p.Name,
            Tr.Format("Monitor_ProcessCpu", p.CpuPercent),
            SizeText.Format(p.MemoryBytes))));
        Pulse++;
    }

    private static IReadOnlyList<double> Push(Queue<double> history, double value)
    {
        history.Enqueue(value);
        while (history.Count > HistoryLength) history.Dequeue();
        return [.. history];
    }

    // Rows change in place when the count matches, so unchanged rows keep their containers.
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        if (target.Count != list.Count)
        {
            target.Clear();
            foreach (var item in list) target.Add(item);
            return;
        }
        for (var i = 0; i < list.Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(target[i], list[i])) target[i] = list[i];
        }
    }
}
