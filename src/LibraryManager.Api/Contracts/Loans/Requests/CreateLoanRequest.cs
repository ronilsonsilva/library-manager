using System.ComponentModel.DataAnnotations;

namespace LibraryManager.Api.Contracts.Loans.Requests;

public sealed record CreateLoanRequest(
    [Required]
    Guid BookId,
    [Required]
    Guid UserId);
