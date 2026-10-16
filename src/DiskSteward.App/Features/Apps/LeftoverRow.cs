namespace DiskSteward.App.Features.Apps;

// Starts unticked: leftovers are shown, never removed on their own.
public sealed partial class LeftoverRow(string path, long size) : ObservableObject
{
    public string Path { get; } = path;
    public long Size { get; } = size;
    public string SizeText { get; } = Localization.SizeText.Format(size);

    [ObservableProperty] public partial bool IsSelected { get; set; }
}
