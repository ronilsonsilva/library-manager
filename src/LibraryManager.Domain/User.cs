using LibraryManager.Domain.Validation;

namespace LibraryManager.Domain;

public sealed class User
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 320;

    private User()
    {
        Name = string.Empty;
        Email = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Email { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Result<User> Create(string name, string email, DateTime utcNow)
    {
        return new DomainGuard()
            .Required(name, ErrorCodes.UserNameRequired, NameMaxLength, ErrorCodes.UserNameTooLong, out var trimmedName)
            .Required(
                email,
                ErrorCodes.UserEmailRequired,
                EmailMaxLength,
                ErrorCodes.UserEmailTooLong,
                out var normalizedEmail,
                static value => value.ToLowerInvariant())
            .ToResult(() => new User
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                Email = normalizedEmail,
                CreatedAtUtc = utcNow
            });
    }
}
