namespace MoleWin.App.Features.Monitor;

// Duration null with IsOn: until turned off.
public sealed record KeepAwakeOption(string Label, bool IsOn, TimeSpan? Duration)
{
    public static IReadOnlyList<KeepAwakeOption> All { get; } =
    [
        new(Tr.Get("Monitor_Awake_Off"), false, null),
        new(Tr.Get("Monitor_Awake_30m"), true, TimeSpan.FromMinutes(30)),
        new(Tr.Get("Monitor_Awake_1h"), true, TimeSpan.FromHours(1)),
        new(Tr.Get("Monitor_Awake_2h"), true, TimeSpan.FromHours(2)),
        new(Tr.Get("Monitor_Awake_4h"), true, TimeSpan.FromHours(4)),
        new(Tr.Get("Monitor_Awake_On"), true, null),
    ];
}
