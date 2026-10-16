namespace MoleWin.Core.Clean;

public enum CleanMode
{
    // Everything inside the folder; the folder itself stays.
    Contents,

    // Files directly in the folder that match the filter.
    Files,
}
