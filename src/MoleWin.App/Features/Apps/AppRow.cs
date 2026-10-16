using MoleWin.Core.Apps;

namespace MoleWin.App.Features.Apps;

public sealed class AppRow(InstalledApp app)
{
    public InstalledApp App { get; } = app;
    public string Name => App.Name;
    public string Detail { get; } = string.Join(", ", new[] { app.Publisher, app.Version }.Where(s => !string.IsNullOrEmpty(s)));
    public string SizeText { get; } = app.SizeBytes > 0 ? Localization.SizeText.Format(app.SizeBytes) : "";
}
