using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace MoleWin.App.Services;

// Theme and accent apply live. High Contrast always wins over the user's choice.
public sealed class ThemeService(AppSettings startupSettings) : IThemeService
{
    private Window? _window;
    private bool _watching;

    public void Attach(Window window)
    {
        _window = window;
        Apply(startupSettings);
    }

    public void Apply(AppSettings settings)
    {
        if (SystemParameters.HighContrast)
        {
            ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);
            return;
        }

        if (settings.Theme == AppTheme.System)
        {
            ApplicationThemeManager.ApplySystemTheme();
            Watch(true);
        }
        else
        {
            Watch(false);
            ApplicationThemeManager.Apply(settings.Theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
        }

        if (settings.Accent is { } hex)
            ApplicationAccentColorManager.Apply((Color)ColorConverter.ConvertFromString(hex), ApplicationThemeManager.GetAppTheme());
        else
            ApplicationAccentColorManager.ApplySystemAccent();
    }

    private void Watch(bool on)
    {
        if (_window is null || on == _watching) return;
        if (on) SystemThemeWatcher.Watch(_window);
        else SystemThemeWatcher.UnWatch(_window);
        _watching = on;
    }
}
