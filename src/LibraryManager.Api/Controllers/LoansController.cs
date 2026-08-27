using LibraryManager.Api.Contracts.Common;
using LibraryManager.Api.Contracts.Loans.Requests;
using LibraryManager.Api.Contracts.Loans.Responses;
using LibraryManager.Api.ModelBinding;
using LibraryManager.Api.ResultMapping;
using LibraryManager.Api.Security;
using LibraryManager.Application.Loans.CancelLoan;
using LibraryManager.Application.Loans.CreateLoan;
using LibraryManager.Application.Loans.ReturnLoan;
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
    public async Task<IActionResult> Create(
        [FromIdempotencyKey] IdempotencyKey idempotencyKey,
        [FromBody] CreateLoanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createLoan.ExecuteAsync(
            request.BookId,
            request.UserId,
            idempotencyKey.Value,
            cancellationToken);
        return result.ToCreatedResult(this, LoanResponse.From);
    }

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<IActionResult> Return(Guid id, CancellationToken cancellationToken)
    {
        var result = await returnLoan.ExecuteAsync(id, cancellationToken);
        return result.ToActionResult(this, LoanResponse.From);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await cancelLoan.ExecuteAsync(id, cancellationToken);
        return result.ToActionResult(this, LoanResponse.From);
    }
}
