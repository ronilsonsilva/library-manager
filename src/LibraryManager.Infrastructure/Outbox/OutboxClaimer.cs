using System.Data;
using System.Data.Common;
using LibraryManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LibraryManager.Infrastructure.Outbox;

public sealed class OutboxClaimer(LibraryDbContext db)
{
    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        string workerId,
        int batchSize,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (batchSize < 1 || string.IsNullOrWhiteSpace(workerId))
        {
            return [];
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (db.Database.GetDbConnection().State != ConnectionState.Open)
            {
                await db.Database.OpenConnectionAsync(cancellationToken);
            }

            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText =
                """
                WITH picked AS (
                    SELECT id
                    FROM outbox_messages
                    WHERE processed_at_utc IS NULL
                      AND next_attempt_at_utc <= NOW()
                      AND (locked_until_utc IS NULL OR locked_until_utc < NOW())
                    ORDER BY next_attempt_at_utc, occurred_at_utc
                    LIMIT @batchSize
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE outbox_messages AS o
                SET locked_by = @workerId,
                    locked_until_utc = NOW() + (@leaseSeconds * INTERVAL '1 second'),
                    attempt_count = attempt_count + 1
                FROM picked
                WHERE o.id = picked.id
                RETURNING o.id, o.type, o.payload_json, o.occurred_at_utc, o.processed_at_utc,
                          o.attempt_count, o.next_attempt_at_utc, o.locked_until_utc, o.locked_by, o.last_error
                """;
            AddParameter(command, "batchSize", batchSize);
            AddParameter(command, "workerId", workerId);
            AddParameter(command, "leaseSeconds", leaseSeconds);

            var claimed = new List<OutboxMessage>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    claimed.Add(ReadMessage(reader));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return claimed;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static OutboxMessage ReadMessage(DbDataReader reader) =>
        new()
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Type = reader.GetString(reader.GetOrdinal("type")),
            PayloadJson = reader.GetValue(reader.GetOrdinal("payload_json"))?.ToString() ?? string.Empty,
            OccurredAtUtc = ReadUtc(reader, "occurred_at_utc")!.Value,
            ProcessedAtUtc = ReadUtc(reader, "processed_at_utc"),
            AttemptCount = reader.GetInt32(reader.GetOrdinal("attempt_count")),
            NextAttemptAtUtc = ReadUtc(reader, "next_attempt_at_utc")!.Value,
            LockedUntilUtc = ReadUtc(reader, "locked_until_utc"),
            LockedBy = ReadOptionalString(reader, "locked_by"),
            LastError = ReadOptionalString(reader, "last_error")
        };

    private static DateTime? ReadUtc(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetDateTime(ordinal);
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static string? ReadOptionalString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)?.ToString();
    }
}
