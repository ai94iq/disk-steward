namespace DiskSteward.Core.Monitor;

public sealed record DriveUsage(string Root, string Format, long Used, long Total, bool Removable)
{
    public double UsedPercent => Total > 0 ? Used * 100.0 / Total : 0;
}
