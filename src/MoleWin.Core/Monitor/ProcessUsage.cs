namespace MoleWin.Core.Monitor;

// CpuPercent is a share of the whole machine (all logical processors), 0..100.
public sealed record ProcessUsage(string Name, double CpuPercent, long MemoryBytes);
