using System.Windows.Data;

namespace MoleWin.App.Common;

// ConverterParameter: one state or a comma-separated list, e.g. 'Idle,Loading,Loaded'.
public sealed class LoadStateToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is LoadState state
            && parameter is string states
            && states.Split(',', StringSplitOptions.TrimEntries).Any(s => Enum.Parse<LoadState>(s) == state)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
