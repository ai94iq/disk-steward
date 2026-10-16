namespace MoleWin.App.Services;

// The only place user-facing dates are formatted. Never use StringFormat for dates in XAML.
// Formatting follows the app culture, which follows the system locale by default.
public sealed class DateFormatter : IDateFormatter
{
    public string Format(DateOnly date, DatePrecision precision)
    {
        if (precision == DatePrecision.Unknown) return string.Empty;
        var value = date.ToDateTime(TimeOnly.MinValue);
        return precision switch
        {
            // "d" = short date ("07/02/2026" or "٢٧/٠٢/١٤٤٧" per locale calendar),
            // "y"/"Y" = month and year ("February 2026" / "فبراير ٢٠٢٦").
            DatePrecision.Day => value.ToString("d", CultureInfo.CurrentCulture),
            DatePrecision.Month => value.ToString("Y", CultureInfo.CurrentCulture),
            DatePrecision.ApproximateYear => Tr.Format("Date_Approx", value.Year),
            _ => value.ToString("Y", CultureInfo.CurrentCulture),
        };
    }
}
