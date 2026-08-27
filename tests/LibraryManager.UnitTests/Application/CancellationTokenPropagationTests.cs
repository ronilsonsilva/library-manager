using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Books;
using LibraryManager.Application.Books.GetBook;
using LibraryManager.Application.Books.ListBooks;
using LibraryManager.Application.Common;
using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Application;

public sealed class CancellationTokenPropagationTests
{
    [Fact]
    public async Task GetBook_observes_a_cancelled_token_before_repository_io()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var useCase = new GetBookUseCase(new MustNotBeCalledBookRepository());

        Func<Task<Result<BookDto>>> execute = () =>
            useCase.ExecuteAsync(Guid.NewGuid(), cancelled.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(execute);
    }

    [Fact]
    public async Task ListBooks_observes_a_cancelled_token_before_repository_io()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var useCase = new ListBooksUseCase(new MustNotBeCalledBookRepository());

        Func<Task<Result<PagedResult<BookDto>>>> execute = () =>
            useCase.ExecuteAsync(1, 20, isActive: null, cancelled.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(execute);
    }

    private sealed class MustNotBeCalledBookRepository : IBookRepository
    {
        public Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Repository must not be called after cancellation.");

        public Task<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Repository must not be called after cancellation.");

        public Task<(IReadOnlyList<Book> Items, int TotalCount)> ListAsync(
            int page,
            int pageSize,
            bool? isActive,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Repository must not be called after cancellation.");

        public Task AddAsync(Book book, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Repository must not be called after cancellation.");

        public Task<int> TryReserveAvailabilityAsync(Guid bookId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Repository must not be called after cancellation.");

        public Task<int> TryRestoreAvailabilityAsync(Guid bookId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Repository must not be called after cancellation.");

        public Task<bool> TryUpdateTotalCopiesAsync(
            Guid bookId,
            int newTotalCopies,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Repository must not be called after cancellation.");
    }
}
