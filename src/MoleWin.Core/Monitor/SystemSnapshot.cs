namespace MoleWin.Core.Monitor;

// One reading of the machine. Rates are per second since the previous reading.
public sealed record SystemSnapshot(
    DeviceInfo Device,
    TimeSpan Uptime,
    int ProcessCount,
    double CpuPercent,
    long MemoryUsed,
    long MemoryTotal,
    IReadOnlyList<DriveUsage> Drives,
    double NetworkDownBytesPerSecond,
    double NetworkUpBytesPerSecond,
    BatteryInfo? Battery,
    IReadOnlyList<ProcessUsage> TopProcesses,
    HealthReport Health)
{
    public double MemoryPercent => MemoryTotal > 0 ? MemoryUsed * 100.0 / MemoryTotal : 0;
}
