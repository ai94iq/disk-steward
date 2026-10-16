namespace MoleWin.Core.Apps;

// One entry from the registry's Uninstall keys. Key identifies it so the app can tell,
// after the uninstaller ran, whether the entry is gone.
public sealed record InstalledApp(
    string Key,
    string Name,
    string? Publisher,
    string? Version,
    long SizeBytes,
    DateTime? InstallDate,
    string UninstallString,
    string? InstallLocation);
