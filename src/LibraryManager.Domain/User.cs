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
        var guard = new DomainGuard();
        guard.Required(name, ErrorCodes.UserNameRequired, out var trimmedName);
        guard.MaxLength(trimmedName, NameMaxLength, ErrorCodes.UserNameTooLong);
        guard.Required(email, ErrorCodes.UserEmailRequired, out var trimmedEmail);
        var normalizedEmail = trimmedEmail.ToLowerInvariant();
        guard.MaxLength(normalizedEmail, EmailMaxLength, ErrorCodes.UserEmailTooLong);

        return guard.ToResult(() => new User
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Email = normalizedEmail,
            CreatedAtUtc = utcNow
        });
    }
}
