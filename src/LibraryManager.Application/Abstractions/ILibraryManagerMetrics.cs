namespace LibraryManager.Application.Abstractions;

public interface ILibraryManagerMetrics
{
    void RecordLoanCreated();

    void RecordLoanUnavailable();

    void RecordIdempotencyReplay();

    void RecordLoanDuration(TimeSpan duration);

    void RecordCacheInvalidationFailure();

    void RecordOutboxProcessed(int count = 1);

    void RecordOutboxFailure();

    void SetOutboxPending(long pending);
}
