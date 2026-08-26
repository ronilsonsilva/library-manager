using LibraryManager.Application.Books;

namespace LibraryManager.Api.Contracts.Books.Responses;

public sealed record BookAvailabilityResponse(
    Guid BookId,
    int AvailableCopies,
    int TotalCopies,
    bool IsActive)
{
    public static BookAvailabilityResponse From(BookAvailabilityDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new(dto.BookId, dto.AvailableCopies, dto.TotalCopies, dto.IsActive);
    }
}
