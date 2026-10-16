using System.Collections.ObjectModel;
using MoleWin.Core.Monitor;

namespace MoleWin.App.Features.Monitor;

// Read-only dashboard: one sample for first paint, then a sample every two seconds while
// the page is on screen.
public sealed partial class MonitorViewModel : PageViewModel
{
    // 30 samples at two seconds: the last minute.
    public const int HistoryLength = 30;
    private const double Gb = 1L << 30;
    private const double Mb = 1L << 20;
    private readonly IMonitorService _monitor;
    private readonly ILogger _log;
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _downHistory = new();
    private CancellationTokenSource? _live;

    public MonitorViewModel(IMonitorService monitor, ILogger<MonitorViewModel> log) : base(log)
    {
        _monitor = monitor;
        _log = log;
        CpuHistory = [];
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
    [ObservableProperty] public partial double MemoryPercent { get; set; }
    [ObservableProperty] public partial string? MemoryPercentText { get; set; }
    [ObservableProperty] public partial LoadLevel MemoryLevel { get; set; }
    [ObservableProperty] public partial string? MemoryText { get; set; }
    [ObservableProperty] public partial string? NetworkText { get; set; }
    [ObservableProperty] public partial IReadOnlyList<double> DownHistory { get; set; }

    // Bumps once per sample; the view pulses the live dot on each change.
    [ObservableProperty] public partial int Pulse { get; set; }
    [ObservableProperty] public partial bool IsLive { get; set; }

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
        UptimeText = Tr.Format("Monitor_Uptime", s.Uptime, s.ProcessCount);

        CpuPercentText = Tr.Format("Monitor_Percent", s.CpuPercent);
        CpuLevel = LoadLevels.Cpu(s.CpuPercent);
        CpuDetail = Tr.Format("Monitor_CpuDetail", s.Device.CpuModel, s.Device.LogicalProcessors);
        CpuHistory = Push(_cpuHistory, s.CpuPercent);

        MemoryPercent = s.MemoryPercent;
        MemoryPercentText = Tr.Format("Monitor_Percent", s.MemoryPercent);
        MemoryLevel = LoadLevels.Fill(s.MemoryPercent);
        MemoryText = Tr.Format("Monitor_MemoryValue", s.MemoryUsed / Gb, s.MemoryTotal / Gb);

        NetworkText = Tr.Format("Monitor_NetworkValue", s.NetworkDownBytesPerSecond / Mb, s.NetworkUpBytesPerSecond / Mb);
        DownHistory = Push(_downHistory, s.NetworkDownBytesPerSecond / Mb);

        Replace(Drives, s.Drives.Select(d => new DriveRow(
            d.Root, d.UsedPercent, LoadLevels.Fill(d.UsedPercent),
            Tr.Format("Monitor_DriveFree", (d.Total - d.Used) / Gb, d.Total / Gb),
            d.Removable ? Tr.Format("Monitor_DriveRemovable", d.Format) : d.Format)));
        Replace(Processes, s.TopProcesses.Select(p => new ProcessRow(
            p.Name, Tr.Format("Monitor_ProcessValue", p.CpuPercent, p.MemoryBytes / Mb))));
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
