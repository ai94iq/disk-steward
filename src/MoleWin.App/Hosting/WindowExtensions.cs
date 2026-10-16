using System.Windows.Media;

namespace MoleWin.App.Hosting;

public static class WindowExtensions
{
    // Call in every window constructor, right after InitializeComponent().
    public static void ApplyCultureDirection(this Window window)
    {
        window.FlowDirection = Culture.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        window.FontFamily = (FontFamily)Application.Current.FindResource("AppFont");
        window.FontSize = 15;
        NumberSubstitution.SetCultureSource(window, NumberCultureSource.Text);
        NumberSubstitution.SetSubstitution(window, Culture.ArabicIndicDigits
            ? NumberSubstitutionMethod.NativeNational
            : NumberSubstitutionMethod.European);
    }
}
