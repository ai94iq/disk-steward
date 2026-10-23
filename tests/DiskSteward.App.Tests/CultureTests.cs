using System.Globalization;
using DiskSteward.App.Hosting;

namespace DiskSteward.App.Tests;

public sealed class CultureTests
{
    [Fact]
    public void A_known_language_is_used() => Assert.Equal("en-US", Culture.Resolve("en-US").Name);

    [Fact]
    public void An_empty_language_follows_the_system() => Assert.Equal(CultureInfo.CurrentCulture, Culture.Resolve(""));

    [Fact]
    public void An_unknown_language_in_settings_falls_back_to_the_system_instead_of_failing_startup() =>
        Assert.Equal(CultureInfo.CurrentCulture, Culture.Resolve("@@not a culture@@"));

    [Theory]
    [InlineData("zz")]
    [InlineData("en-XX")]
    public void A_well_formed_name_Windows_does_not_ship_also_follows_the_system(string language) =>
        Assert.Equal(CultureInfo.CurrentCulture, Culture.Resolve(language));
}
