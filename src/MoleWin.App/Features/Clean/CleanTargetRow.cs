using MoleWin.Core.Clean;

namespace MoleWin.App.Features.Clean;

// One rule's findings. Starts unticked: nothing is chosen for the user.
public sealed partial class CleanTargetRow(CleanFinding finding, string label) : ObservableObject
{
    private const int TooltipPaths = 15;

    public CleanFinding Finding { get; } = finding;
    public string Label { get; } = label;
    public long Bytes => Finding.Size;
    public string SizeText { get; } = Localization.SizeText.Format(finding.Size);
    public string ItemsText { get; } = Tr.Plural("Clean_Items", finding.Items.Count);

    // A preview of what would go, for the tooltip.
    public string PathsPreview { get; } = string.Join(Environment.NewLine, finding.Items.Take(TooltipPaths).Select(i => i.Path))
        + (finding.Items.Count > TooltipPaths ? Environment.NewLine + "..." : "");

    [ObservableProperty] public partial double Share { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }
}
