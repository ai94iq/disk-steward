using System.Collections.ObjectModel;
using MoleWin.App.Resources;
using MoleWin.Core.Clean;

namespace MoleWin.App.Features.Clean;

// A group of targets, collapsed until opened. Ticking the group ticks every target in it.
public sealed partial class CleanCategoryRow(CleanCategory category) : ObservableObject
{
    public CleanCategory Category { get; } = category;
    public string Name { get; } = Tr.Get($"Clean_Category_{category}");
    public ObservableCollection<CleanTargetRow> Targets { get; } = [];

    public IconKind Icon { get; } = category switch
    {
        CleanCategory.System => IconKind.WindowWrench,
        CleanCategory.Browsers => IconKind.GlobeDesktop,
        CleanCategory.Apps => IconKind.AppFolder,
        CleanCategory.Gpu => IconKind.Cube,
        CleanCategory.Developer => IconKind.CodeBlock,
        _ => IconKind.BotSparkle,
    };

    [ObservableProperty] public partial long Bytes { get; set; }
    [ObservableProperty] public partial string? SizeText { get; set; }
    [ObservableProperty] public partial string? ItemsText { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial bool IsExpanded { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        foreach (var target in Targets) target.IsSelected = value;
    }

    public void Add(CleanTargetRow row)
    {
        Targets.Add(row);
        Bytes += row.Bytes;
        SizeText = Localization.SizeText.Format(Bytes);
        ItemsText = Tr.Plural("Clean_Places", Targets.Count);
    }
}
