using System.Windows.Media;
using DiskSteward.Core.Apps;

namespace DiskSteward.App.Features.Apps;

public sealed partial class AppRow(InstalledApp app, IDateFormatter dates) : ObservableObject
{
    public InstalledApp App { get; } = app;
    public string Name => App.Name;
    public string Detail { get; } = string.Join(", ", new[] { app.Publisher, app.Version }.Where(s => !string.IsNullOrEmpty(s)));
    // Many installers report neither; an empty cell would read as a failed load.
    public bool HasSize => App.SizeBytes > 0;
    public bool HasDate => App.InstallDate is not null;
    public string SizeText { get; } = app.SizeBytes > 0 ? Localization.SizeText.Format(app.SizeBytes) : Tr.Get("Apps_NotReported");
    public string DateText { get; } = app.InstallDate is { } date ? dates.Format(DateOnly.FromDateTime(date), DatePrecision.Day) : Tr.Get("Apps_NotReported");

    // Filled in the background after the list shows; until then the row has the generic icon.
    [ObservableProperty] public partial ImageSource? Icon { get; set; }
}
