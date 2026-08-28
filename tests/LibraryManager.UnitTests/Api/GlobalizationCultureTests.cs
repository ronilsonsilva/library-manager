using System.Globalization;

namespace LibraryManager.UnitTests.Api;

public sealed class GlobalizationCultureTests
{
    [Fact]
    public void Supported_cultures_are_available_for_localization()
    {
        var english = new CultureInfo("en-US");
        var portuguese = new CultureInfo("pt-BR");

        Assert.Equal("en-US", english.Name);
        Assert.Equal("pt-BR", portuguese.Name);
        Assert.NotEqual(english.DisplayName, portuguese.DisplayName);
    }
}
