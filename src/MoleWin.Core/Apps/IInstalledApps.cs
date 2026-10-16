namespace MoleWin.Core.Apps;

public interface IInstalledApps
{
    IReadOnlyList<InstalledApp> List();

    // True while the app's Uninstall entry still exists.
    bool IsInstalled(InstalledApp app);
}
