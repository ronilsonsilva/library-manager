using LibraryManager.Application.Abstractions;
using LibraryManager.Domain;

namespace LibraryManager.Application.Books;

public sealed record BookAvailabilityDto(
    Guid BookId,
    int AvailableCopies,
    int TotalCopies,
    bool IsActive)
{
    public static BookAvailabilityDto From(Book book) =>
        new(book.Id, book.AvailableCopies, book.TotalCopies, book.IsActive);

    public static BookAvailabilityDto From(BookAvailabilityCacheItem item) =>
        new(item.BookId, item.AvailableCopies, item.TotalCopies, item.IsActive);
}
