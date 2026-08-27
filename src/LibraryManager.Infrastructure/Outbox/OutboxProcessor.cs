using System.Text.Json;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Infrastructure.Caching;
using LibraryManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Infrastructure.Outbox;

public sealed class OutboxProcessor : BackgroundService
{
    public const int DefaultBatchSize = 10;
    public const int DefaultLeaseSeconds = 30;
    public const int DefaultPollIntervalMilliseconds = 2000;
    public const int DefaultMaxBackoffSeconds = 60;
    public const int LastErrorMaxLength = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly IAvailabilityCache _cache;
    private readonly IClock _clock;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly ILibraryManagerMetrics _metrics;
    private readonly string _workerId;
    private readonly int _batchSize;
    private readonly int _leaseSeconds;
    private readonly int _maxBackoffSeconds;
    private readonly TimeSpan _pollInterval;

    public OutboxProcessor(
        IServiceScopeFactory scopes,
        [FromKeyedServices(RedisAvailabilityCache.ServiceKey)] IAvailabilityCache cache,
        IClock clock,
        IConfiguration configuration,
        ILogger<OutboxProcessor> logger,
        ILibraryManagerMetrics metrics)
    {
        _scopes = scopes;
        _cache = cache;
        _clock = clock;
        _logger = logger;
        _metrics = metrics;
        _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        if (_workerId.Length > 128)
        {
            _workerId = _workerId[..128];
        }

        _batchSize = ParseBounded(configuration["Outbox:BatchSize"], DefaultBatchSize, 1, 100);
        _leaseSeconds = ParseBounded(configuration["Outbox:LeaseSeconds"], DefaultLeaseSeconds, 1, 300);
        _maxBackoffSeconds = ParseBounded(
            configuration["Outbox:MaxBackoffSeconds"],
            DefaultMaxBackoffSeconds,
            1,
            3600);
        _pollInterval = TimeSpan.FromMilliseconds(
            ParseBounded(
                configuration["Outbox:PollIntervalMilliseconds"],
                DefaultPollIntervalMilliseconds,
                50,
                60_000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(_workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox processor batch failed");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task<int> ProcessBatchAsync(string workerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<OutboxMessage> claimed;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var claimer = scope.ServiceProvider.GetRequiredService<OutboxClaimer>();
            claimed = await claimer.ClaimAsync(workerId, _batchSize, _leaseSeconds, cancellationToken);
        }

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ConsumeAsync(message, cancellationToken);
                await MarkProcessedAsync(message.Id, cancellationToken);
                _metrics.RecordOutboxProcessed();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Outbox message {MessageId} failed; scheduling retry",
                    message.Id);
                await MarkFailedAsync(message, exception, cancellationToken);
                _metrics.RecordOutboxFailure();
            }
        }

        await RefreshPendingAsync(cancellationToken);
        return claimed.Count;
    }

    private async Task ConsumeAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.Type, AvailabilityOutbox.MessageType, StringComparison.Ordinal))
        {
            _logger.LogWarning("Skipping unknown outbox type {Type} for {MessageId}", message.Type, message.Id);
            return;
        }

        var payload = JsonSerializer.Deserialize<AvailabilityChangedPayload>(message.PayloadJson, JsonOptions);
        if (payload is null || payload.BookId == Guid.Empty)
        {
            throw new InvalidOperationException("Outbox payload is missing bookId.");
        }

        await _cache.RemoveAsync(payload.BookId, cancellationToken);
    }

    private async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var processedAt = _clock.UtcNow;
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.OutboxMessages
            .Where(message => message.Id == messageId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(message => message.ProcessedAtUtc, processedAt)
                    .SetProperty(message => message.LockedBy, (string?)null)
                    .SetProperty(message => message.LockedUntilUtc, (DateTime?)null)
                    .SetProperty(message => message.LastError, (string?)null),
                cancellationToken);
    }

    private async Task MarkFailedAsync(OutboxMessage message, Exception exception, CancellationToken cancellationToken)
    {
        var nextAttempt = _clock.UtcNow.Add(OutboxBackoff.Compute(message.AttemptCount, _maxBackoffSeconds));
        var lastError = Truncate(exception.Message, LastErrorMaxLength);
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.OutboxMessages
            .Where(item => item.Id == message.Id)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(item => item.LastError, lastError)
                    .SetProperty(item => item.NextAttemptAtUtc, nextAttempt)
                    .SetProperty(item => item.LockedBy, (string?)null)
                    .SetProperty(item => item.LockedUntilUtc, (DateTime?)null),
                cancellationToken);
    }

    private async Task RefreshPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var pending = await db.OutboxMessages.CountAsync(
            message => message.ProcessedAtUtc == null,
            cancellationToken);
        _metrics.SetOutboxPending(pending);
    }

    private static int ParseBounded(string? value, int fallback, int min, int max) =>
        int.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record AvailabilityChangedPayload(Guid BookId, string? CorrelationId);
}
