using LibraryManager.Domain;

namespace LibraryManager.UnitTests;

public class AssemblySmokeTests
{
    [Fact]
    public void Domain_BookType_IsAvailable()
    {
        Assert.Equal("Book", nameof(Book));
    }
}
