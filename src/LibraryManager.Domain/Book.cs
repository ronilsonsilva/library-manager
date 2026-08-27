using LibraryManager.Domain.Validation;

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

    public static Result<Book> Create(string title, string isbn, string author, int totalCopies, DateTime utcNow)
    {
        var guard = new DomainGuard();
        guard.Required(title, ErrorCodes.BookTitleRequired, out var normalizedTitle);
        guard.MaxLength(normalizedTitle, TitleMaxLength, ErrorCodes.BookTitleTooLong);
        guard.Required(isbn, ErrorCodes.BookIsbnRequired, out var normalizedIsbn);
        guard.MaxLength(normalizedIsbn, IsbnMaxLength, ErrorCodes.BookIsbnTooLong);
        guard.Required(author, ErrorCodes.BookAuthorRequired, out var normalizedAuthor);
        guard.MaxLength(normalizedAuthor, AuthorMaxLength, ErrorCodes.BookAuthorTooLong);
        guard.Positive(totalCopies, ErrorCodes.BookTotalCopiesInvalid);

        return guard.ToResult(() => new Book
        {
            Id = Guid.NewGuid(),
            Title = normalizedTitle,
            Isbn = normalizedIsbn,
            Author = normalizedAuthor,
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            TotalCopies = totalCopies,
            AvailableCopies = totalCopies
        });
    }

    public Result UpdateCatalog(string title, string author, DateTime utcNow)
    {
        var guard = new DomainGuard();
        guard.Required(title, ErrorCodes.BookTitleRequired, out var normalizedTitle);
        guard.MaxLength(normalizedTitle, TitleMaxLength, ErrorCodes.BookTitleTooLong);
        guard.Required(author, ErrorCodes.BookAuthorRequired, out var normalizedAuthor);
        guard.MaxLength(normalizedAuthor, AuthorMaxLength, ErrorCodes.BookAuthorTooLong);

        var outcome = guard.ToResult();
        if (outcome.IsFailure)
        {
            return outcome;
        }

        Title = normalizedTitle;
        Author = normalizedAuthor;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result ApplyTotalCopies(int totalCopies, DateTime utcNow)
    {
        if (totalCopies < BorrowedCopies)
        {
            return Result.Failure(Error.BusinessRule(ErrorCodes.BookTotalCopiesBelowBorrowed));
        }

        var guard = new DomainGuard();
        guard.Positive(totalCopies, ErrorCodes.BookTotalCopiesInvalid);
        var outcome = guard.ToResult();
        if (outcome.IsFailure)
        {
            return outcome;
        }

        var borrowed = BorrowedCopies;
        TotalCopies = totalCopies;
        AvailableCopies = totalCopies - borrowed;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        UpdatedAtUtc = utcNow;
    }
}
