using LibraryManager.Api.Contracts.Loans.Requests;
using LibraryManager.Api.Contracts.Loans.Responses;
using LibraryManager.Api.Security;
using LibraryManager.Application.Loans.CancelLoan;
using LibraryManager.Application.Loans.CreateLoan;
using LibraryManager.Application.Loans.ReturnLoan;
using LibraryManager.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.Api.Controllers;

[ApiController]
[Route("loans")]
public sealed class LoansController(
    CreateLoanUseCase createLoan,
    ReturnLoanUseCase returnLoan,
    CancelLoanUseCase cancelLoan) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<LoanResponse>> Create(
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
        return StatusCode(StatusCodes.Status201Created, LoanResponse.From(loan));
    }

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<LoanResponse>> Return(Guid id, CancellationToken cancellationToken)
    {
        var loan = await returnLoan.ExecuteAsync(id, cancellationToken);
        return Ok(LoanResponse.From(loan));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<LoanResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var loan = await cancelLoan.ExecuteAsync(id, cancellationToken);
        return Ok(LoanResponse.From(loan));
    }
}
