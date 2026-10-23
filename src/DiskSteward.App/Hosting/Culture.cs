using System.Windows.Markup;

namespace DiskSteward.App.Hosting;

public static class Culture
{
    public static bool IsRtl { get; private set; }

    public static bool ArabicIndicDigits { get; private set; }

    public static void Configure(AppSettings settings)
    {
        var culture = Resolve(settings.Language);

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

    // Empty Language = follow the system locale (dates, numbers, calendar, direction). A name
    // Windows does not know (a hand-edited settings.json) also follows the system, rather than
    // stopping the app before it can show anything.
    public static CultureInfo Resolve(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return CultureInfo.CurrentCulture;
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentCulture;
        }
    }
}
