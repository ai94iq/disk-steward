namespace DiskSteward.App.Services;

public interface IKeepAwake
{
    // Keeps the PC and display awake for the given time, or until Stop when null.
    void Start(TimeSpan? duration);

    void Stop();

    // Raised when a timed request runs out, so the screen stops showing it as on.
    event EventHandler? Ended;
}
