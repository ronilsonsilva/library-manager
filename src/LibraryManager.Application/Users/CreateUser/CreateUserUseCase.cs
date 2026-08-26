using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Users;
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
    public async Task<UserDto> ExecuteAsync(string name, string email, CancellationToken cancellationToken)
    {
        var existing = await users.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessRuleException("A user with this email already exists.");
        }

        var utcNow = clock.UtcNow;
        var user = User.Create(name, email, utcNow);
        await users.AddAsync(user, cancellationToken);

        var audit = AuditEvent.Create(
            AuditMetadata.UserEntity,
            user.Id,
            AuditMetadata.UserCreated,
            currentUser.ActorId,
            utcNow,
            correlation.CorrelationId,
            JsonPayload.Serialize(new { user.Name, user.Email }));
        await audits.AddAsync(audit, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserDto.From(user);
    }
}
