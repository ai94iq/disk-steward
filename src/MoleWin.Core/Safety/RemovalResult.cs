namespace MoleWin.Core.Safety;

public sealed record RemovalResult(string Path, RemovalOutcome Outcome, string? Error = null);
