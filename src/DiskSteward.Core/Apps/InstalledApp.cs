namespace DiskSteward.Core.Apps;

// One entry from the registry's Uninstall keys. Key identifies it so the app can tell,
// after the uninstaller ran, whether the entry is gone. IconPath is the raw DisplayIcon
// value ("path" or "path,index", sometimes quoted).
public sealed record InstalledApp(
    string Key,
    string Name,
    string? Publisher,
    string? Version,
    long SizeBytes,
    DateTime? InstallDate,
    string UninstallString,
    string? InstallLocation,
    string? IconPath = null);
