using System.Diagnostics;
using MoleWin.Core.Apps;

namespace MoleWin.App.Services;

// Runs the uninstaller exactly as registered, with its own window and no silent switches,
// so the user sees the vendor's steps. ShellExecute lets Windows show the admin prompt when
// the uninstaller asks for it; MoleWin itself never runs elevated.
public sealed class UninstallerRunner : IUninstallerRunner
{
    public async Task RunAsync(InstalledApp app, CancellationToken ct)
    {
        var command = UninstallCommand.Parse(app.UninstallString);
        using var process = Process.Start(new ProcessStartInfo(command.FileName, command.Arguments) { UseShellExecute = true });
        if (process is not null) await process.WaitForExitAsync(ct);
    }
}
