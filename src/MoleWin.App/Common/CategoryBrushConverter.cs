using System.Windows.Data;
using System.Windows.Media;
using MoleWin.Core.Clean;
using Wpf.Ui.Appearance;

namespace MoleWin.App.Common;

// A Clean category's identity color: slots 1 to 6 of the dataviz reference categorical
// palette in fixed order, with the step for the current theme. Validated with the dataviz
// validator: dark set passes all checks on #1a1a19; light set passes with the contrast WARN
// relieved by the category name always shown next to the icon.
public sealed class CategoryBrushConverter : IValueConverter
{
    private static readonly Brush[] Light = Freeze("#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300");
    private static readonly Brush[] Dark = Freeze("#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300");

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
