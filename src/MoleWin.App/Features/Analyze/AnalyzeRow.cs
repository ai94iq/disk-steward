namespace MoleWin.App.Features.Analyze;

// One child of the folder on screen. Mutable: a folder's size arrives after it is listed,
// and the tick box changes with the user.
public sealed partial class AnalyzeRow(string name, string path, bool isDirectory, bool isProtected) : ObservableObject
{
    public string Name { get; } = name;
    public string Path { get; } = path;
    public bool IsDirectory { get; } = isDirectory;

    // Protected rows show but can never be ticked (see ProtectedPaths).
    public bool IsProtected { get; } = isProtected;
    public bool CanSelect => !IsProtected;

    [ObservableProperty] public partial long Size { get; set; }
    [ObservableProperty] public partial string? SizeText { get; set; }
    [ObservableProperty] public partial bool IsMeasuring { get; set; }
    [ObservableProperty] public partial double Share { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && IsProtected) IsSelected = false;
    }
}
