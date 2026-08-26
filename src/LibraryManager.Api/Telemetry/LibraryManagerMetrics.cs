using System.Diagnostics.Metrics;
using LibraryManager.Application.Abstractions;

namespace LibraryManager.Api.Telemetry;

public sealed class LibraryManagerMetrics : ILibraryManagerMetrics
{
    public const string MeterName = "LibraryManager";

    private readonly Counter<long> _loansCreated;
    private readonly Counter<long> _loansUnavailable;
    private readonly Counter<long> _idempotencyReplays;
    private readonly Histogram<double> _loanDuration;
    private readonly Counter<long> _cacheInvalidationFailures;
    private readonly Counter<long> _outboxProcessed;
    private readonly Counter<long> _outboxFailures;
    private long _outboxPending;

    public LibraryManagerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _loansCreated = meter.CreateCounter<long>("library_manager_loans_created");
        _loansUnavailable = meter.CreateCounter<long>("library_manager_loans_unavailable");
        _idempotencyReplays = meter.CreateCounter<long>("library_manager_idempotency_replays");
        _loanDuration = meter.CreateHistogram<double>("library_manager_loan_duration", unit: "ms");
        _cacheInvalidationFailures = meter.CreateCounter<long>("library_manager_cache_invalidation_failures");
        _outboxProcessed = meter.CreateCounter<long>("library_manager_outbox_processed");
        _outboxFailures = meter.CreateCounter<long>("library_manager_outbox_failures");
        meter.CreateObservableGauge(
            "library_manager_outbox_pending",
            () => Interlocked.Read(ref _outboxPending));
    }

    public void RecordLoanCreated() => _loansCreated.Add(1);

    public void RecordLoanUnavailable() => _loansUnavailable.Add(1);

    public void RecordIdempotencyReplay() => _idempotencyReplays.Add(1);

    public void RecordLoanDuration(TimeSpan duration) =>
        _loanDuration.Record(duration.TotalMilliseconds);

    public void RecordCacheInvalidationFailure() => _cacheInvalidationFailures.Add(1);

    public void RecordOutboxProcessed(int count = 1) => _outboxProcessed.Add(count);

    public void RecordOutboxFailure() => _outboxFailures.Add(1);

    public void SetOutboxPending(long pending) => Interlocked.Exchange(ref _outboxPending, pending);
}
