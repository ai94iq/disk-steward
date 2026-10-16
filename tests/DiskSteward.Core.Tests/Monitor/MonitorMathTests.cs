using DiskSteward.Core.Monitor;

namespace DiskSteward.Core.Tests.Monitor;

public sealed class MonitorMathTests
{
    [Fact]
    public void Cpu_is_the_busy_share_of_kernel_plus_user_time()
    {
        // 1000 ticks passed (600 kernel incl. 250 idle, 400 user): 750 busy.
        var percent = MonitorMath.CpuPercent(new CpuTimes(0, 0, 0), new CpuTimes(250, 600, 400));

        Assert.Equal(75, percent, 3);
    }

    [Fact]
    public void Cpu_with_no_elapsed_time_is_zero()
    {
        Assert.Equal(0, MonitorMath.CpuPercent(new CpuTimes(5, 5, 5), new CpuTimes(5, 5, 5)));
    }

    [Fact]
    public void Top_processes_group_by_name_and_rank_by_cpu()
    {
        var top = MonitorMath.TopProcesses(
            [new ProcessUsage("chrome", 0.5, 100), new ProcessUsage("Chrome", 0.25, 50), new ProcessUsage("idle", 0, 900), new ProcessUsage("code", 0.5, 10)],
            2);

        Assert.Equal([new ProcessUsage("chrome", 0.75, 150, 2), new ProcessUsage("code", 0.5, 10)], top);
    }

    [Theory]
    [InlineData(1000, 3000, 2, 1000)]
    [InlineData(3000, 1000, 2, 0)]
    public void Rate_is_per_second_and_ignores_counter_resets(long previous, long current, int seconds, double expected)
    {
        Assert.Equal(expected, MonitorMath.Rate(previous, current, TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Quiet_machine_scores_full_health()
    {
        Assert.Equal(new HealthReport(100, HealthIssue.None), MonitorMath.Health(20, 50, 60));
    }

    [Fact]
    public void Full_disk_costs_the_most_and_is_named()
    {
        var report = MonitorMath.Health(cpuPercent: 80, memoryPercent: 50, fullestDrivePercent: 100);

        Assert.Equal(HealthIssue.Disk, report.Issue);
        Assert.Equal(100 - 30 - 8, report.Score);
    }
}
