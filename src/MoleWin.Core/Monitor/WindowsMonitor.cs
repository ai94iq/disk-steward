using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MoleWin.Core.Monitor;

// Reads the machine with plain Windows APIs as the normal user: no WMI, no admin, no services.
// Not thread-safe: one sampler per reader, called from one loop.
[SupportedOSPlatform("windows")]
public sealed class WindowsMonitor : IMonitorService
{
    private const int TopProcessCount = 5;
    private const byte NoBattery = 128;
    private readonly Lazy<DeviceInfo> _device = new(ReadDevice);
    private readonly Dictionary<int, TimeSpan> _processCpu = [];
    private CpuTimes _cpu;
    private long _down;
    private long _up;
    private long _at;

    public SystemSnapshot Sample()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = _at == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(_at, now);
        _at = now;

        var cpu = ReadCpuTimes();
        var cpuPercent = _cpu == default ? 0 : MonitorMath.CpuPercent(_cpu, cpu);
        _cpu = cpu;

        var (down, up) = ReadNetworkBytes();
        var downRate = elapsed == TimeSpan.Zero ? 0 : MonitorMath.Rate(_down, down, elapsed);
        var upRate = elapsed == TimeSpan.Zero ? 0 : MonitorMath.Rate(_up, up, elapsed);
        (_down, _up) = (down, up);

        var (used, total) = ReadMemory();
        var drives = ReadDrives();
        var processes = ReadTopProcesses(elapsed, out var processCount);
        var fullest = drives.Where(d => !d.Removable).Select(d => d.UsedPercent).DefaultIfEmpty(0).Max();

        return new SystemSnapshot(
            _device.Value,
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            processCount,
            cpuPercent,
            used,
            total,
            drives,
            downRate,
            upRate,
            ReadBattery(),
            processes,
            MonitorMath.Health(cpuPercent, total > 0 ? used * 100.0 / total : 0, fullest));
    }

    private static CpuTimes ReadCpuTimes() =>
        NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user) ? new CpuTimes(idle, kernel, user) : default;

    private static (long Used, long Total) ReadMemory()
    {
        var status = new NativeMethods.MemoryStatusEx { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        return NativeMethods.GlobalMemoryStatusEx(ref status)
            ? ((long)(status.TotalPhys - status.AvailPhys), (long)status.TotalPhys)
            : (0, 0);
    }

    private static List<DriveUsage> ReadDrives() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable)
            .Select(d => new DriveUsage(d.RootDirectory.FullName, d.DriveFormat, d.TotalSize - d.TotalFreeSpace, d.TotalSize, d.DriveType == DriveType.Removable))
            .ToList();

    // Adapters that are up; loopback and tunnels would double count.
    private static (long Down, long Up) ReadNetworkBytes()
    {
        long down = 0, up = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up
                || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
            var stats = nic.GetIPStatistics();
            down += stats.BytesReceived;
            up += stats.BytesSent;
        }
        return (down, up);
    }

    private static BatteryInfo? ReadBattery() =>
        NativeMethods.GetSystemPowerStatus(out var power) && power.BatteryFlag != NoBattery && power.BatteryLifePercent <= 100
            ? new BatteryInfo(power.BatteryLifePercent, power.AcLineStatus == 1)
            : null;

    // CPU share from each process's processor time since the last sample. Processes the
    // user may not read (system services) are skipped rather than failing the sample.
    private List<ProcessUsage> ReadTopProcesses(TimeSpan elapsed, out int count)
    {
        var processes = Process.GetProcesses();
        count = processes.Length;
        var seen = new Dictionary<int, TimeSpan>(processes.Length);
        var usage = new List<ProcessUsage>(processes.Length);
        var budget = elapsed.TotalMilliseconds * Environment.ProcessorCount;
        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    var cpu = process.TotalProcessorTime;
                    seen[process.Id] = cpu;
                    var share = budget > 0 && _processCpu.TryGetValue(process.Id, out var before)
                        ? Math.Clamp((cpu - before).TotalMilliseconds * 100 / budget, 0, 100)
                        : 0;
                    usage.Add(new ProcessUsage(process.ProcessName, share, process.WorkingSet64));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Access denied or the process exited mid-read.
                }
            }
        }
        _processCpu.Clear();
        foreach (var (id, cpu) in seen) _processCpu[id] = cpu;
        return MonitorMath.TopProcesses(usage, TopProcessCount);
    }

    private static DeviceInfo ReadDevice()
    {
        using var bios = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
        using var cpu = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        using var os = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var model = bios?.GetValue("SystemProductName") as string ?? Environment.MachineName;
        var cpuName = (cpu?.GetValue("ProcessorNameString") as string)?.Trim() ?? "";
        // ProductName still says "Windows 10" on Windows 11; the build number tells them apart.
        var edition = os?.GetValue("EditionID") as string ?? "";
        var release = os?.GetValue("DisplayVersion") as string ?? "";
        var name = Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10";
        return new DeviceInfo(model, $"{name} {edition} {release}".Trim(), cpuName, Environment.ProcessorCount);
    }
}
