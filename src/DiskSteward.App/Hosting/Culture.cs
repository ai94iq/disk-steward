using System.Windows.Markup;

namespace DiskSteward.App.Hosting;

public static class Culture
{
    public static bool IsRtl { get; private set; }

    public static bool ArabicIndicDigits { get; private set; }

    public static void Configure(AppSettings settings)
    {
        // Empty Language = follow the system locale (dates, numbers, calendar, direction).
        var culture = string.IsNullOrWhiteSpace(settings.Language)
            ? CultureInfo.CurrentCulture
            : CultureInfo.GetCultureInfo(settings.Language);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        IsRtl = culture.TextInfo.IsRightToLeft;
        ArabicIndicDigits = settings.ArabicIndicDigits;

        // WPF formats bindings as en-US unless this is overridden. Allowed once per process.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }
}
