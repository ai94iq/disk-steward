using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace MoleWin.App.Services;

// SetThreadExecutionState is per thread, so every call, including the timer that ends a timed
// request, runs on the UI thread that made the first one. Windows clears the request when the
// app exits.
public sealed class KeepAwake : IKeepAwake
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;
    private readonly DispatcherTimer _timer = new();

    public KeepAwake()
    {
        _timer.Tick += (_, _) => Stop();
    }

    public void Start(TimeSpan? duration)
    {
        _timer.Stop();
        SetThreadExecutionState(EsContinuous | EsSystemRequired | EsDisplayRequired);
        if (duration is not { } time) return;
        _timer.Interval = time;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        SetThreadExecutionState(EsContinuous);
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
