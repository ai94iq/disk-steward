namespace DiskSteward.Core.Monitor;

public sealed record HealthReport(int Score, HealthIssue Issue);
