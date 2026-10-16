namespace MoleWin.Core.Monitor;

public interface IMonitorService
{
    // Each call reads the machine; rates and CPU compare against the previous call.
    SystemSnapshot Sample();
}
