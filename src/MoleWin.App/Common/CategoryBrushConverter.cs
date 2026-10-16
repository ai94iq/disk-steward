using System.Windows.Data;
using System.Windows.Media;
using MoleWin.Core.Clean;
using Wpf.Ui.Appearance;

namespace MoleWin.App.Common;

// A Clean category's identity color, from the dataviz reference categorical palette with the
// step for the current theme. No category is green: green is the "safe to remove" signal.
// In category order (System, Browsers, Apps, Gpu, Developer, AiTools): blue, orange, violet,
// aqua, yellow, magenta. Validated with the dataviz validator: all checks pass in both modes,
// the light set's contrast WARN relieved by the category name always shown beside the icon.
public sealed class CategoryBrushConverter : IValueConverter
{
    private static readonly Brush[] Light = Freeze("#2a78d6", "#eb6834", "#6250d6", "#1baf7a", "#eda100", "#e87ba4");
    private static readonly Brush[] Dark = Freeze("#3987e5", "#d95926", "#9085e9", "#199e70", "#c98500", "#d55181");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CleanCategory category) return null;
        var set = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light ? Light : Dark;
        return set[(int)category % set.Length];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush[] Freeze(params string[] hex) =>
    [
        .. hex.Select(h =>
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h));
            brush.Freeze();
            return (Brush)brush;
        }),
    ];
}
