namespace DiskSteward.Core.Monitor;

// CpuPercent is a share of the whole machine (all logical processors), 0..100.
// Count: how many processes share the name once grouped (a browser's tabs, say).
public sealed record ProcessUsage(string Name, double CpuPercent, long MemoryBytes, int Count = 1);
