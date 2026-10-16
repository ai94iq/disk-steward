using System.Windows.Media;
using MoleWin.Core.Apps;

namespace MoleWin.App.Features.Apps;

public sealed partial class AppRow(InstalledApp app, IDateFormatter dates) : ObservableObject
{
    public InstalledApp App { get; } = app;
    public string Name => App.Name;
    public string Detail { get; } = string.Join(", ", new[] { app.Publisher, app.Version }.Where(s => !string.IsNullOrEmpty(s)));
    public string SizeText { get; } = app.SizeBytes > 0 ? Localization.SizeText.Format(app.SizeBytes) : "";
    public string DateText { get; } = app.InstallDate is { } date ? dates.Format(DateOnly.FromDateTime(date), DatePrecision.Day) : "";

    // Filled in the background after the list shows; until then the row has the generic icon.
    [ObservableProperty] public partial ImageSource? Icon { get; set; }
}
