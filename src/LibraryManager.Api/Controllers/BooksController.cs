using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.Application.Books.CreateBook;
using LibraryManager.Application.Books.DeactivateBook;
using LibraryManager.Application.Books.GetBook;
using LibraryManager.Application.Books.ListBooks;
using LibraryManager.Application.Books.UpdateBook;
using LibraryManager.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.Api.Controllers;

[ApiController]
[Route("books")]
public sealed class BooksController(
    CreateBookUseCase createBook,
    GetBookUseCase getBook,
    ListBooksUseCase listBooks,
    UpdateBookUseCase updateBook,
    DeactivateBookUseCase deactivateBook) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<BookDto>> Create(
        [FromBody] CreateBookRequest request,
        CancellationToken cancellationToken)
    {
        var book = await createBook.ExecuteAsync(
            request.Title,
            request.Isbn,
            request.Author,
            request.TotalCopies,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<BookDto>>> List(
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await listBooks.ExecuteAsync(page, pageSize, isActive, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<BookDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var book = await getBook.ExecuteAsync(id, cancellationToken);
        return Ok(book);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<BookDto>> Update(
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

        return Ok(book);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await deactivateBook.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}

public sealed record CreateBookRequest(string Title, string Isbn, string Author, int TotalCopies);

public sealed record UpdateBookRequest(string Title, string Author, int TotalCopies);
