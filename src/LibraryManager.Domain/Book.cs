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
        return new DomainGuard()
            .Required(title, ErrorCodes.BookTitleRequired, TitleMaxLength, ErrorCodes.BookTitleTooLong, out var normalizedTitle)
            .Required(isbn, ErrorCodes.BookIsbnRequired, IsbnMaxLength, ErrorCodes.BookIsbnTooLong, out var normalizedIsbn)
            .Required(author, ErrorCodes.BookAuthorRequired, AuthorMaxLength, ErrorCodes.BookAuthorTooLong, out var normalizedAuthor)
            .Positive(totalCopies, ErrorCodes.BookTotalCopiesInvalid)
            .ToResult(() => new Book
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
        return new DomainGuard()
            .Required(title, ErrorCodes.BookTitleRequired, TitleMaxLength, ErrorCodes.BookTitleTooLong, out var normalizedTitle)
            .Required(author, ErrorCodes.BookAuthorRequired, AuthorMaxLength, ErrorCodes.BookAuthorTooLong, out var normalizedAuthor)
            .Apply(() =>
            {
                Title = normalizedTitle;
                Author = normalizedAuthor;
                UpdatedAtUtc = utcNow;
            });
    }

    public Result ApplyTotalCopies(int totalCopies, DateTime utcNow)
    {
        var borrowed = BorrowedCopies;
        return new DomainGuard()
            .Ensure(totalCopies >= borrowed, Error.BusinessRule(ErrorCodes.BookTotalCopiesBelowBorrowed))
            .Positive(totalCopies, ErrorCodes.BookTotalCopiesInvalid)
            .Apply(() =>
            {
                TotalCopies = totalCopies;
                AvailableCopies = totalCopies - borrowed;
                UpdatedAtUtc = utcNow;
            });
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        UpdatedAtUtc = utcNow;
    }
}
