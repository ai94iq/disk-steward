namespace MoleWin.Core.Monitor;

// The biggest single drag on the health score; None when nothing stands out.
public enum HealthIssue
{
    None,
    Cpu,
    Memory,
    Disk,
}
