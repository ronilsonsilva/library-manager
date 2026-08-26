using LibraryManager.Application.Abstractions;
using LibraryManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Infrastructure.Persistence.Repositories;

public sealed class LoanRepository(LibraryDbContext db) : ILoanRepository
{
    public Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Loans.FirstOrDefaultAsync(loan => loan.Id == id, cancellationToken);

    public async Task AddAsync(Loan loan, CancellationToken cancellationToken)
    {
        await db.Loans.AddAsync(loan, cancellationToken);
    }

    public Task<(IReadOnlyList<Loan> Items, int TotalCount)> ListByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        ListAsync(loan => loan.UserId == userId, page, pageSize, cancellationToken);

    public Task<(IReadOnlyList<Loan> Items, int TotalCount)> ListByBookAsync(
        Guid bookId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        ListAsync(loan => loan.BookId == bookId, page, pageSize, cancellationToken);

    public async Task<int> TryCompleteActiveAsync(
        Guid loanId,
        LoanStatus terminalStatus,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        var status = terminalStatus.ToString();
        var rows = terminalStatus switch
        {
            LoanStatus.Returned => await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE loans
                 SET status = {status},
                     returned_at_utc = {completedAtUtc}
                 WHERE id = {loanId}
                   AND status = {nameof(LoanStatus.Active)}
                 """,
                cancellationToken),
            LoanStatus.Cancelled => await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE loans
                 SET status = {status},
                     cancelled_at_utc = {completedAtUtc}
                 WHERE id = {loanId}
                   AND status = {nameof(LoanStatus.Active)}
                 """,
                cancellationToken),
            _ => 0
        };

        if (rows == 1)
        {
            var tracked = await db.Loans.FindAsync([loanId], cancellationToken);
            if (tracked is not null)
            {
                await db.Entry(tracked).ReloadAsync(cancellationToken);
            }
        }

        return rows;
    }

    private async Task<(IReadOnlyList<Loan> Items, int TotalCount)> ListAsync(
        System.Linq.Expressions.Expression<Func<Loan, bool>> predicate,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Loans.AsNoTracking().Where(predicate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(loan => loan.BorrowedAtUtc)
            .ThenBy(loan => loan.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
