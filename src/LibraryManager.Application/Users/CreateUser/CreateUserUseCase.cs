using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;

namespace LibraryManager.Application.Users.CreateUser;

public sealed class CreateUserUseCase(
    IUserRepository users,
    IAuditRepository audits,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUserContext currentUser,
    ICorrelationContext correlation)
{
    public async Task<Result<UserDto>> ExecuteAsync(string name, string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await users.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<UserDto>(Error.BusinessRule(ErrorCodes.UserDuplicateEmail));
        }

        var utcNow = clock.UtcNow;
        var created = User.Create(name, email, utcNow);
        if (created.IsFailure)
        {
            return created.AsFailure<UserDto>();
        }

        var user = created.Value;
        await users.AddAsync(user, cancellationToken);

        var audit = AuditEvent.Create(
            AuditMetadata.UserEntity,
            user.Id,
            AuditMetadata.UserCreated,
            currentUser.ActorId,
            utcNow,
            correlation.CorrelationId,
            JsonPayload.Serialize(new { user.Name, user.Email }));
        if (audit.IsFailure)
        {
            return audit.AsFailure<UserDto>();
        }

        await audits.AddAsync(audit.Value, cancellationToken);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.AsFailure<UserDto>();
        }

        return Result.Success(UserDto.From(user));
    }
}
