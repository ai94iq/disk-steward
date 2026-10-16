using System.Collections.ObjectModel;
using MoleWin.Core.Clean;

namespace MoleWin.App.Features.Clean;

// A group of targets. Ticking the group ticks every target in it.
public sealed partial class CleanCategoryRow(CleanCategory category) : ObservableObject
{
    public CleanCategory Category { get; } = category;
    public string Name { get; } = Tr.Get($"Clean_Category_{category}");
    public ObservableCollection<CleanTargetRow> Targets { get; } = [];

    [ObservableProperty] public partial long Bytes { get; set; }
    [ObservableProperty] public partial string? SizeText { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        foreach (var target in Targets) target.IsSelected = value;
    }

    public void Add(CleanTargetRow row)
    {
        Targets.Add(row);
        Bytes += row.Bytes;
        SizeText = Localization.SizeText.Format(Bytes);
    }
}
