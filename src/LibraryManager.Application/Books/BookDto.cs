using LibraryManager.Domain;

namespace LibraryManager.Application.Books;

public sealed record BookDto(
    Guid Id,
    string Title,
    string Isbn,
    string Author,
    int TotalCopies,
    int AvailableCopies,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static BookDto From(Book book) =>
        new(
            book.Id,
            book.Title,
            book.Isbn,
            book.Author,
            book.TotalCopies,
            book.AvailableCopies,
            book.IsActive,
            book.CreatedAtUtc,
            book.UpdatedAtUtc);
}
