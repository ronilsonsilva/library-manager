namespace LibraryManager.Domain;

public sealed class Book
{
    public const int TitleMaxLength = 500;
    public const int IsbnMaxLength = 32;
    public const int AuthorMaxLength = 500;

    private Book()
    {
        Title = string.Empty;
        Isbn = string.Empty;
        Author = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string Isbn { get; private set; }

    public string Author { get; private set; }

    public int TotalCopies { get; private set; }

    public int AvailableCopies { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public int BorrowedCopies => TotalCopies - AvailableCopies;

    public static Book Create(string title, string isbn, string author, int totalCopies, DateTime utcNow)
    {
        var book = new Book
        {
            Id = Guid.NewGuid(),
            Title = RequireText(title, TitleMaxLength, nameof(title)),
            Isbn = RequireText(isbn, IsbnMaxLength, nameof(isbn)),
            Author = RequireText(author, AuthorMaxLength, nameof(author)),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        if (totalCopies < 1)
        {
            throw new DomainException("TotalCopies must be at least 1.");
        }

        book.TotalCopies = totalCopies;
        book.AvailableCopies = totalCopies;
        return book;
    }

    public void UpdateCatalog(string title, string author, DateTime utcNow)
    {
        Title = RequireText(title, TitleMaxLength, nameof(title));
        Author = RequireText(author, AuthorMaxLength, nameof(author));
        UpdatedAtUtc = utcNow;
    }

    public void ApplyTotalCopies(int totalCopies, DateTime utcNow)
    {
        if (totalCopies < BorrowedCopies)
        {
            throw new DomainException("TotalCopies cannot be below the number of copies currently on loan.");
        }

        if (totalCopies < 1)
        {
            throw new DomainException("TotalCopies must be at least 1.");
        }

        var borrowed = BorrowedCopies;
        TotalCopies = totalCopies;
        AvailableCopies = totalCopies - borrowed;
        UpdatedAtUtc = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        UpdatedAtUtc = utcNow;
    }

    private static string RequireText(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }
}
