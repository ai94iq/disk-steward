using System.Collections.ObjectModel;
using DiskSteward.Core.Analyze;
using DiskSteward.Core.Monitor;
using DiskSteward.Core.Safety;

namespace DiskSteward.App.Features.Analyze;

// One drive, folder or file in the Analyze tree. Folders list their children the first time
// they are expanded; until then a placeholder child gives the tree its expand arrow.
public sealed partial class FolderNode : ObservableObject
{
    private readonly Func<FolderNode, Task>? _expand;

    public FolderNode(FolderEntry entry, FolderKind kind, FolderNode? parent, ProtectionReason protection, Func<FolderNode, Task>? expand)
    {
        Name = entry.Name;
        Path = entry.Path;
        Kind = kind;
        Parent = parent;
        Protection = protection;
        IsSystem = entry.IsSystem;
        _expand = expand;
        if (kind is FolderKind.Drive or FolderKind.Folder) Children.Add(Placeholder());
        if (entry.Capacity is { } capacity && entry.Size is { } used)
        {
            UsedPercent = capacity > 0 ? used * 100.0 / capacity : 0;
            Level = LoadLevels.Fill(UsedPercent);
            FreeText = Tr.Format("Analyze_DriveFree", FormatSize(capacity - used), FormatSize(capacity));
        }
        if (entry.Size is { } size) SetSize(size);
    }

    private FolderNode()
    {
        Name = "";
        Path = "";
        Kind = FolderKind.Placeholder;
    }

    public string Name { get; }
    public string Path { get; }
    public FolderKind Kind { get; }
    public FolderNode? Parent { get; }
    public ProtectionReason Protection { get; }
    public bool IsProtected => Protection != ProtectionReason.None;
    public bool IsSystem { get; }

    // Why a locked row cannot be ticked, in the user's terms; null when it can.
    public string? LockText => IsSystem && !IsProtected ? Tr.Get("Analyze_Lock_VolumeSystem")
        : Protection switch
        {
            ProtectionReason.None => null,
            ProtectionReason.Windows or ProtectionReason.Programs or ProtectionReason.ProgramData
                or ProtectionReason.AppOwnData or ProtectionReason.PersonalFolder or ProtectionReason.VolumeSystem
                => Tr.Get($"Analyze_Lock_{Protection}"),
            _ => Tr.Get("Analyze_Protected"),
        };
    public bool IsDrive => Kind == FolderKind.Drive;

    // Drives only expand; protected and system items show but are never ticked.
    public bool CanSelect => Kind is FolderKind.Folder or FolderKind.File && !IsProtected && !IsSystem;

    public double UsedPercent { get; }
    public LoadLevel Level { get; }
    public string? FreeText { get; }
    public bool IsLoaded { get; set; }

    public ObservableCollection<FolderNode> Children { get; } = [];

    [ObservableProperty] public partial long Size { get; set; }
    [ObservableProperty] public partial string? SizeText { get; set; }
    [ObservableProperty] public partial double Share { get; set; }
    [ObservableProperty] public partial bool IsMeasuring { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial bool IsExpanded { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && !CanSelect) IsSelected = false;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !IsLoaded && _expand is not null) _ = _expand(this);
    }

    public void SetSize(long size)
    {
        Size = size;
        SizeText = FormatSize(size);
    }

    private static FolderNode Placeholder() => new();

    private static string FormatSize(long bytes) => Localization.SizeText.Format(bytes);
}
