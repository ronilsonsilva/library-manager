using LibraryManager.Application.Abstractions;
using LibraryManager.Infrastructure.Persistence;

namespace LibraryManager.Infrastructure.Outbox;

public sealed class OutboxWriter(LibraryDbContext db) : IOutboxWriter
{
    public Task WriteAsync(
        string type,
        string payloadJson,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            PayloadJson = payloadJson,
            OccurredAtUtc = occurredAtUtc,
            AttemptCount = 0,
            NextAttemptAtUtc = occurredAtUtc
        });

        return Task.CompletedTask;
    }
}
