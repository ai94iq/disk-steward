namespace MoleWin.App.Services;

public interface IDateFormatter
{
    string Format(DateOnly date, DatePrecision precision);
}
