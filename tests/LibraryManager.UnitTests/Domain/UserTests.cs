using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Domain;

public sealed class UserTests
{
    [Fact]
    public void Create_stores_trimmed_name_and_lowercase_email()
    {
        var now = DateTime.UtcNow;

        var user = User.Create("  Ada Lovelace  ", "  Ada@Example.COM  ", now).Value;

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
        var result = User.Create(name, "ada@example.com", DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.UserNameRequired, result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_email(string email)
    {
        var result = User.Create("Ada Lovelace", email, DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.UserEmailRequired, result.Error.Code);
    }

    [Fact]
    public void Create_rejects_name_longer_than_max_length()
    {
        var name = new string('a', User.NameMaxLength + 1);

        var result = User.Create(name, "ada@example.com", DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.UserNameTooLong, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }
}
