namespace DiskSteward.Core.Abstractions;

// Sent after a successful write so open pages can reload. Area examples: "customer", "order".
public sealed record DataChanged(string Area, long? Id = null);
