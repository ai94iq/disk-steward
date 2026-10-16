using System.Collections.ObjectModel;
using DiskSteward.Core.Safety;

namespace DiskSteward.App.Common;

// What a page did, newest first: scans started and finished, removals, and anything that
// went wrong. Results the page already shows (sizes, found items) stay out of it.
public sealed class ActivityLog : ObservableCollection<ActivityEntry>
{
    private const int MaxEntries = 300;

    public void Add(string text, bool isProblem = false)
    {
        Insert(0, new ActivityEntry(DateTime.Now, text, isProblem));
        while (Count > MaxEntries) RemoveAt(Count - 1);
    }

    // One summary line for what went to the Recycle Bin, then one line per path that did not.
    public void AddRemovals(IReadOnlyList<RemovalResult> results)
    {
        var recycled = results.Count(r => r.Outcome == RemovalOutcome.Recycled);
        if (recycled > 0) Add(Tr.Format("Common_LogRecycled", recycled));
        foreach (var result in results)
        {
            switch (result.Outcome)
            {
                case RemovalOutcome.Protected:
                    Add(Tr.Format("Analyze_LogProtected", result.Path), isProblem: true);
                    break;
                case RemovalOutcome.NotRecyclable:
                    Add(Tr.Format("Analyze_LogNotRecyclable", result.Path), isProblem: true);
                    break;
                case RemovalOutcome.Failed:
                    Add(Tr.Format("Analyze_LogFailed", result.Path, result.Error), isProblem: true);
                    break;
            }
        }
    }
}
