using System.Windows.Data;
using MoleWin.Core.Monitor;

namespace MoleWin.App.Common;

// LoadLevel to a theme brush. ConverterParameter 'Text' keeps normal text in the primary text
// color; otherwise normal uses the accent (bars, charts). High and critical use the theme's
// caution and critical fills so they follow light, dark and high contrast.
public sealed class LoadLevelBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            LoadLevel.High => "SystemFillColorCautionBrush",
            LoadLevel.Critical => "SystemFillColorCriticalBrush",
            _ when parameter is "Text" => "TextFillColorPrimaryBrush",
            _ => "AccentFillColorDefaultBrush",
        };
        return Application.Current.TryFindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
