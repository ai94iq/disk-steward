namespace MoleWin.App.Services;

public interface IKeepAwake
{
    // Keeps the PC and display awake for the given time, or until Stop when null.
    void Start(TimeSpan? duration);

    void Stop();
}
