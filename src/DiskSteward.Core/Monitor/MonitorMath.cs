namespace DiskSteward.Core.Monitor;

// Pure arithmetic behind the readings, kept apart from the Windows calls so it can be tested.
public static class MonitorMath
{
    // Busy share of the time between two GetSystemTimes readings. Kernel time includes idle.
    public static double CpuPercent(CpuTimes previous, CpuTimes current)
    {
        var idle = current.Idle - previous.Idle;
        var total = current.Kernel - previous.Kernel + (current.User - previous.User);
        return total <= 0 ? 0 : Math.Clamp((total - idle) * 100.0 / total, 0, 100);
    }

    // Per-second rate between two cumulative counters; counter resets read as zero.
    public static double Rate(long previous, long current, TimeSpan elapsed) =>
        elapsed <= TimeSpan.Zero || current < previous ? 0 : (current - previous) / elapsed.TotalSeconds;

    // Starts at 100 and loses points only for sustained pressure: CPU above 70%, memory above
    // 80%, the fullest fixed drive above 85%. Each penalty grows linearly to its cap at 100%.
    public static HealthReport Health(double cpuPercent, double memoryPercent, double fullestDrivePercent)
    {
        var cpu = Penalty(cpuPercent, 70, 25);
        var memory = Penalty(memoryPercent, 80, 25);
        var disk = Penalty(fullestDrivePercent, 85, 30);
        var score = (int)Math.Round(100 - cpu - memory - disk);
        var worst = Math.Max(cpu, Math.Max(memory, disk));
        var issue = worst <= 0 ? HealthIssue.None : worst == disk ? HealthIssue.Disk : worst == memory ? HealthIssue.Memory : HealthIssue.Cpu;
        return new HealthReport(Math.Clamp(score, 0, 100), issue);
    }

    // Processes grouped by name as Task Manager does, busiest first, then largest.
    public static List<ProcessUsage> TopProcesses(IEnumerable<ProcessUsage> processes, int count) =>
        processes
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProcessUsage(g.First().Name, g.Sum(p => p.CpuPercent), g.Sum(p => p.MemoryBytes), g.Sum(p => p.Count)))
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.MemoryBytes)
            .Take(count)
            .ToList();

    private static double Penalty(double percent, double from, double cap) =>
        percent <= from ? 0 : Math.Min(percent - from, 100 - from) / (100 - from) * cap;
}
