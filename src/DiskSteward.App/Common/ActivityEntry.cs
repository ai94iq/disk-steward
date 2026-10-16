namespace DiskSteward.App.Common;

// One line in a page's Details panel. IsProblem marks what the user should look at: a
// removal that was refused or failed.
public sealed record ActivityEntry(DateTime Time, string Text, bool IsProblem = false)
{
    public string TimeText => Time.ToString("t", CultureInfo.CurrentCulture);
}
