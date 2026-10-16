namespace DiskSteward.App.Services;

public interface IDateFormatter
{
    string Format(DateOnly date, DatePrecision precision);
}
