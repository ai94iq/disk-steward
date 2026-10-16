using CommunityToolkit.Mvvm.Messaging;

namespace MoleWin.App.Services;

// Repositories call this from background threads, so it hops to the UI thread before messaging.
public sealed class DataChangeNotifier : IDataChangeNotifier
{
    public void Notify(DataChanged change) =>
        Application.Current.Dispatcher.InvokeAsync(() => WeakReferenceMessenger.Default.Send(change));
}
