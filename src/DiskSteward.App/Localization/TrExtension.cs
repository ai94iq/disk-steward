using System.Windows.Markup;

namespace DiskSteward.App.Localization;

// XAML: xmlns:l="clr-namespace:DiskSteward.App.Localization", then Text="{l:Tr Common_Save}".
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Tr.Get(Key);
}
