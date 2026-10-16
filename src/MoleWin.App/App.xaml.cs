using Microsoft.Extensions.Hosting;
using Serilog;
using MoleWin.App.Shell;

namespace MoleWin.App;

public partial class App : Application
{
    private IHost? _host;
    private Mutex? _singleInstance;
    private bool _ownsMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = SettingsStore.Load();
        Culture.Configure(settings);              // before any window or Tr call
        AppLogging.Configure();
        HookGlobalExceptionHandlers();

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\MoleWin.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            await new DialogService().ShowInfoAsync(Tr.Get("App_AlreadyRunning"));
            Shutdown();
            return;
        }

        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Services.AddSerilog();        // uses the static Log.Logger
            builder.Services.AddAppServices(settings);
#if DEBUG
            Sandbox.Apply(builder.Services);
#endif
            _host = builder.Build();
            await _host.StartAsync();

            var database = _host.Services.GetRequiredService<DatabaseInitializer>();
            await Task.Run(database.BackupAndMigrate);

            _host.Services.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            await new DialogService().ShowErrorAsync(Tr.Get("Error_Startup"));
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();                         // the app registers no hosted services
        if (_ownsMutex) _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void HookGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, a) =>
        {
            Log.Error(a.Exception, "Unhandled UI exception");
            _ = new DialogService().ShowErrorAsync(Tr.Get("Error_Unexpected"));
            a.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            Log.Fatal(a.ExceptionObject as Exception, "Fatal non-UI exception");
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            Log.Error(a.Exception, "Unobserved task exception");
            a.SetObserved();
        };
    }
}
