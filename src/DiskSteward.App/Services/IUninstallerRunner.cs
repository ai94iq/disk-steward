using DiskSteward.Core.Apps;

namespace DiskSteward.App.Services;

public interface IUninstallerRunner
{
    // Starts the app's own uninstaller, visibly, and waits for that process to exit.
    Task RunAsync(InstalledApp app, CancellationToken ct);
}
