using LibraryManager.Application.Books;

namespace LibraryManager.Api.Contracts.Books.Responses;

public sealed record BookResponse(
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
    public static BookResponse From(BookDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new(
            dto.Id,
            dto.Title,
            dto.Isbn,
            dto.Author,
            dto.TotalCopies,
            dto.AvailableCopies,
            dto.IsActive,
            dto.CreatedAtUtc,
            dto.UpdatedAtUtc);
    }
}
