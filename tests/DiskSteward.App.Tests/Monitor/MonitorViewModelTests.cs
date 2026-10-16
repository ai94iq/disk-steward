using DiskSteward.App.Features.Monitor;
using DiskSteward.App.Services;
using DiskSteward.Core.Monitor;
using NSubstitute;

namespace DiskSteward.App.Tests.Monitor;

public sealed class MonitorViewModelTests
{
    private static SystemSnapshot Snapshot(double cpu = 12, int health = 92, long driveUsed = 25, BatteryInfo? battery = null) => new(
        new DeviceInfo("Laptop", "Windows 11 Pro", "Intel i7", 16),
        TimeSpan.FromHours(1),
        300,
        cpu,
        8L << 30,
        16L << 30,
        [new DriveUsage(@"C:\", "NTFS", driveUsed, 100, false)],
        1L << 20,
        0,
        battery,
        [new ProcessUsage("a", 50, 100L << 20), new ProcessUsage("b", 10, 50L << 20)],
        new HealthReport(health, HealthIssue.None));

    private static MonitorViewModel Create(IMonitorService monitor, IKeepAwake? awake = null) =>
        new(monitor, awake ?? Substitute.For<IKeepAwake>(), NullLogger<MonitorViewModel>.Instance) { Interval = TimeSpan.FromMilliseconds(1) };

    [Fact]
    public void Keep_awake_starts_off_and_follows_the_choice()
    {
        var awake = Substitute.For<IKeepAwake>();
        var vm = Create(Monitor(Snapshot()), awake);
        Assert.False(vm.KeepAwake.IsOn);

        vm.KeepAwake = KeepAwakeOption.All.Single(o => o.Duration == TimeSpan.FromHours(1));
        awake.Received(1).Start(TimeSpan.FromHours(1));

        vm.KeepAwake = KeepAwakeOption.All.Single(o => o.IsOn && o.Duration is null);
        awake.Received(1).Start(null);

        vm.KeepAwake = KeepAwakeOption.All[0];
        awake.Received(1).Stop();
    }

    private static IMonitorService Monitor(params SystemSnapshot[] samples)
    {
        var monitor = Substitute.For<IMonitorService>();
        monitor.Sample().Returns(samples[0], samples[1..]);
        return monitor;
    }

    [Fact]
    public async Task First_sample_fills_every_section()
    {
        var vm = Create(Monitor(Snapshot()));

        await vm.EnsureLoadedAsync();

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal("Laptop", vm.Device);
        Assert.Equal([50], vm.MemoryHistory);
        Assert.Single(vm.Drives);
        Assert.Equal(2, vm.Processes.Count);
        Assert.Equal("50.0%", vm.Processes[0].CpuText);
        Assert.False(string.IsNullOrEmpty(vm.NetworkText));
    }

    [Fact]
    public async Task Load_levels_color_the_busy_parts()
    {
        var vm = Create(Monitor(Snapshot(cpu: 95, health: 40, driveUsed: 96)));

        await vm.EnsureLoadedAsync();

        Assert.Equal(LoadLevel.Critical, vm.CpuLevel);
        Assert.Equal(LoadLevel.Critical, vm.HealthLevel);
        Assert.Equal(LoadLevel.Critical, vm.Drives[0].Level);
    }

    [Fact]
    public async Task Live_samples_update_values_and_grow_the_history()
    {
        var vm = Create(Monitor(Snapshot(cpu: 10), Snapshot(cpu: 40), Snapshot(cpu: 80)));
        await vm.EnsureLoadedAsync();

        await vm.RunLiveAsync(TestContext.Current.CancellationToken, samples: 2);

        Assert.Equal([10, 40, 80], vm.CpuHistory);
        Assert.Equal(LoadLevel.High, vm.CpuLevel);
        Assert.Equal(3, vm.Pulse);
        Assert.False(vm.IsLive);
    }

    [Fact]
    public async Task History_keeps_the_last_minute()
    {
        var samples = Enumerable.Range(0, MonitorViewModel.HistoryLength + 5).Select(i => Snapshot(cpu: i)).ToArray();
        var vm = Create(Monitor(samples));
        await vm.EnsureLoadedAsync();

        await vm.RunLiveAsync(TestContext.Current.CancellationToken, samples: samples.Length - 1);

        Assert.Equal(MonitorViewModel.HistoryLength, vm.CpuHistory.Count);
        Assert.Equal(samples[^1].CpuPercent, vm.CpuHistory[^1]);
    }

    [Fact]
    public async Task A_failed_sample_keeps_the_page_and_the_next_one_lands()
    {
        var monitor = Substitute.For<IMonitorService>();
        var calls = 0;
        monitor.Sample().Returns(_ => ++calls == 2 ? throw new InvalidOperationException("busy") : Snapshot(cpu: calls == 1 ? 10 : 30));
        var vm = Create(monitor);
        await vm.EnsureLoadedAsync();

        await vm.RunLiveAsync(TestContext.Current.CancellationToken, samples: 2);

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal([10, 30], vm.CpuHistory);
    }

    [Fact]
    public async Task Battery_shows_only_on_machines_that_have_one()
    {
        var desktop = Create(Monitor(Snapshot()));
        var laptop = Create(Monitor(Snapshot(battery: new BatteryInfo(77, true))));

        await desktop.EnsureLoadedAsync();
        await laptop.EnsureLoadedAsync();

        Assert.DoesNotContain("77", desktop.DeviceDetail, StringComparison.Ordinal);
        Assert.Contains("77", laptop.DeviceDetail, StringComparison.Ordinal);
    }
}
