namespace DiskSteward.App.Services;

// Every info, error and confirm dialog goes through here so RTL is handled once.
public sealed class DialogService : IDialogService
{
    public Task ShowInfoAsync(string message)
    {
        Show(message, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string message)
    {
        Show(message, MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string message) =>
        Task.FromResult(Show(message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);

    private static MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage icon)
    {
        var options = Culture.IsRtl
            ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
            : MessageBoxOptions.None;
        var defaultResult = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.OK;
        return MessageBox.Show(message, Tr.Get("App_Name"), buttons, icon, defaultResult, options);
    }
}
