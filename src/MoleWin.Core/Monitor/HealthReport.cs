namespace MoleWin.Core.Monitor;

public sealed record HealthReport(int Score, HealthIssue Issue);
