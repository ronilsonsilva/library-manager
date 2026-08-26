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

    public static User Create(string name, string email, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.");
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException($"Name must be at most {NameMaxLength} characters.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length > EmailMaxLength)
        {
            throw new DomainException($"Email must be at most {EmailMaxLength} characters.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Email = normalizedEmail,
            CreatedAtUtc = utcNow
        };
    }
}
