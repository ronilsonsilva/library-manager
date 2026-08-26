using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Domain;

public sealed class UserTests
{
    [Fact]
    public void Create_stores_trimmed_name_and_lowercase_email()
    {
        var now = DateTime.UtcNow;

        var user = User.Create("  Ada Lovelace  ", "  Ada@Example.COM  ", now);

        Assert.Equal("Ada Lovelace", user.Name);
        Assert.Equal("ada@example.com", user.Email);
        Assert.Equal(now, user.CreatedAtUtc);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_name(string name)
    {
        Assert.Throws<DomainException>(() => User.Create(name, "ada@example.com", DateTime.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_email(string email)
    {
        Assert.Throws<DomainException>(() => User.Create("Ada Lovelace", email, DateTime.UtcNow));
    }

    [Fact]
    public void Create_rejects_name_longer_than_max_length()
    {
        var name = new string('a', User.NameMaxLength + 1);

        var exception = Assert.Throws<DomainException>(() =>
            User.Create(name, "ada@example.com", DateTime.UtcNow));

        Assert.Contains("Name", exception.Message, StringComparison.Ordinal);
    }
}
