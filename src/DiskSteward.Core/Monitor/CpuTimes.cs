namespace DiskSteward.Core.Monitor;

// GetSystemTimes values in 100 ns ticks; Kernel includes Idle.
public readonly record struct CpuTimes(long Idle, long Kernel, long User);
