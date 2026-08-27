using System.ComponentModel.DataAnnotations;

namespace LibraryManager.Api.Contracts.Loans.Requests;

public sealed record CreateLoanRequest(
    [Required(ErrorMessage = "Validation_BookId_Required")]
    Guid BookId,
    [Required(ErrorMessage = "Validation_UserId_Required")]
    Guid UserId);
