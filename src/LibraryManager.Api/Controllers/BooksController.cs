using LibraryManager.Api.Contracts.Books.Requests;
using LibraryManager.Api.Contracts.Books.Responses;
using LibraryManager.Api.Contracts.Common;
using LibraryManager.Api.Contracts.Loans.Responses;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books.CreateBook;
using LibraryManager.Application.Books.DeactivateBook;
using LibraryManager.Application.Books.GetBook;
using LibraryManager.Application.Books.GetBookAvailability;
using LibraryManager.Application.Books.ListBooks;
using LibraryManager.Application.Books.UpdateBook;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans.GetBookLoanHistory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.Api.Controllers;

[ApiController]
[Route("books")]
public sealed class BooksController(
    CreateBookUseCase createBook,
    GetBookUseCase getBook,
    GetBookAvailabilityUseCase getBookAvailability,
    GetBookLoanHistoryUseCase getBookLoanHistory,
    ListBooksUseCase listBooks,
    UpdateBookUseCase updateBook,
    DeactivateBookUseCase deactivateBook) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<BookResponse>> Create(
        [FromBody] CreateBookRequest request,
        CancellationToken cancellationToken)
    {
        var book = await createBook.ExecuteAsync(
            request.Title,
            request.Isbn,
            request.Author,
            request.TotalCopies,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = book.Id }, BookResponse.From(book));
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResponse<BookResponse>>> List(
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await listBooks.ExecuteAsync(page, pageSize, isActive, cancellationToken);
        return Ok(PagedResponse<BookResponse>.From(result, BookResponse.From));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<BookResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var book = await getBook.ExecuteAsync(id, cancellationToken);
        return Ok(BookResponse.From(book));
    }

    [HttpGet("{id:guid}/availability")]
    [Authorize]
    public async Task<ActionResult<BookAvailabilityResponse>> GetAvailability(Guid id, CancellationToken cancellationToken)
    {
        var availability = await getBookAvailability.ExecuteAsync(id, cancellationToken);
        return Ok(BookAvailabilityResponse.From(availability));
    }

    [HttpGet("{id:guid}/loans")]
    [HttpGet("{id:guid}/history")]
    [Authorize]
    public async Task<ActionResult<PagedResponse<LoanResponse>>> GetLoanHistory(
        Guid id,
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await getBookLoanHistory.ExecuteAsync(id, page, pageSize, cancellationToken);
        return Ok(PagedResponse<LoanResponse>.From(result, LoanResponse.From));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<BookResponse>> Update(
        Guid id,
        [FromBody] UpdateBookRequest request,
        CancellationToken cancellationToken)
    {
        var book = await updateBook.ExecuteAsync(
            id,
            request.Title,
            request.Author,
            request.TotalCopies,
            cancellationToken);

        return Ok(BookResponse.From(book));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await deactivateBook.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}
