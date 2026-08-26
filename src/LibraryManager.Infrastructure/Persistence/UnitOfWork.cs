using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LibraryManager.Infrastructure.Persistence;

public sealed class UnitOfWork(LibraryDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
                                                  && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw postgres.ConstraintName switch
            {
                "ux_books_isbn" => new BusinessRuleException("A book with this ISBN already exists."),
                "ux_users_email" => new BusinessRuleException("A user with this email already exists."),
                "ux_loans_user_book_active" => new BusinessRuleException(
                    "An active loan already exists for this user and book."),
                _ => exception
            };
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
