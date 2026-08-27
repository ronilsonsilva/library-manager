using LibraryManager.Application.Abstractions;
using LibraryManager.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LibraryManager.Infrastructure.Persistence;

public sealed class UnitOfWork(LibraryDbContext db) : IUnitOfWork
{
    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
                                                  && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            Result? mapped = postgres.ConstraintName switch
            {
                "ux_books_isbn" => Result.Failure(Error.BusinessRule(ErrorCodes.BookDuplicateIsbn)),
                "ux_users_email" => Result.Failure(Error.BusinessRule(ErrorCodes.UserDuplicateEmail)),
                "ux_loans_user_book_active" => Result.Failure(Error.BusinessRule(ErrorCodes.LoanDuplicateActive)),
                _ => null
            };

            if (mapped is null)
            {
                throw;
            }

            return mapped;
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
            if (result is IResult { IsFailure: true })
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

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
