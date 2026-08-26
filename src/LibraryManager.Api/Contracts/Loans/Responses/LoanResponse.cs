using LibraryManager.Application.Loans;

namespace LibraryManager.Api.Contracts.Loans.Responses;

public sealed record LoanResponse(
    Guid Id,
    Guid BookId,
    Guid UserId,
    string Status,
    DateTime BorrowedAtUtc,
    DateTime DueAtUtc,
    DateTime? ReturnedAtUtc,
    DateTime? CancelledAtUtc)
{
    public static LoanResponse From(LoanDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new(
            dto.Id,
            dto.BookId,
            dto.UserId,
            dto.Status,
            dto.BorrowedAtUtc,
            dto.DueAtUtc,
            dto.ReturnedAtUtc,
            dto.CancelledAtUtc);
    }
}
