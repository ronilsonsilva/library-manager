using LibraryManager.Api.Security;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Loans.CreateLoan;
using LibraryManager.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.Api.Controllers;

[ApiController]
[Route("loans")]
public sealed class LoansController(CreateLoanUseCase createLoan) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<LoanDto>> Create(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CreateLoanRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("Idempotency-Key is required.");
        }

        var key = idempotencyKey.Trim();
        if (key.Length > 128)
        {
            throw new DomainException("Idempotency-Key must be at most 128 characters.");
        }

        var loan = await createLoan.ExecuteAsync(request.BookId, request.UserId, key, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, loan);
    }
}

public sealed record CreateLoanRequest(Guid BookId, Guid UserId);
