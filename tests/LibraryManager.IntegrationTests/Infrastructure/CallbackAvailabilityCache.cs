using LibraryManager.Application.Abstractions;
using LibraryManager.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LibraryManager.IntegrationTests.Infrastructure;

internal sealed class CallbackAvailabilityCache(IAvailabilityCache inner) : IAvailabilityCache
{
    public int RemainingRemoveFailures { get; set; }

    public bool FailAllRemoves { get; set; }

    public bool FailAllGets { get; set; }

    public bool FailAllSets { get; set; }

    public Guid? FailRemoveForBookId { get; set; }

    public Func<Guid, CancellationToken, Task>? OnRemove { get; set; }

    public async Task<BookAvailabilityCacheItem?> GetAsync(Guid bookId, CancellationToken cancellationToken)
    {
        if (FailAllGets)
        {
            throw SimulatedRedisFailure();
        }

        return await inner.GetAsync(bookId, cancellationToken);
    }

    public async Task SetAsync(BookAvailabilityCacheItem item, CancellationToken cancellationToken)
    {
        if (FailAllSets)
        {
            throw SimulatedRedisFailure();
        }

        await inner.SetAsync(item, cancellationToken);
    }

    public async Task RemoveAsync(Guid bookId, CancellationToken cancellationToken)
    {
        if (OnRemove is not null)
        {
            await OnRemove(bookId, cancellationToken);
        }

        var shouldFail = FailAllRemoves
            || (RemainingRemoveFailures > 0
                && (FailRemoveForBookId is null || FailRemoveForBookId == bookId));
        if (shouldFail)
        {
            if (!FailAllRemoves)
            {
                RemainingRemoveFailures--;
            }

            throw SimulatedRedisFailure();
        }

        await inner.RemoveAsync(bookId, cancellationToken);
    }

    public void Reset()
    {
        RemainingRemoveFailures = 0;
        FailAllRemoves = false;
        FailAllGets = false;
        FailAllSets = false;
        FailRemoveForBookId = null;
        OnRemove = null;
    }

    public static void Register(IServiceCollection services, Action<CallbackAvailabilityCache>? configure = null)
    {
        foreach (var descriptor in services.Where(item => item.ServiceType == typeof(IAvailabilityCache)).ToList())
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(sp =>
        {
            var cache = new CallbackAvailabilityCache(sp.GetRequiredService<RedisAvailabilityCache>());
            configure?.Invoke(cache);
            return cache;
        });
        services.AddKeyedSingleton<IAvailabilityCache>(
            RedisAvailabilityCache.ServiceKey,
            (sp, _) => sp.GetRequiredService<CallbackAvailabilityCache>());
        services.AddSingleton<IAvailabilityCache>(sp =>
            new ResilientAvailabilityCacheDecorator(
                sp.GetRequiredService<CallbackAvailabilityCache>(),
                sp.GetRequiredService<ILogger<ResilientAvailabilityCacheDecorator>>(),
                sp.GetRequiredService<ILibraryManagerMetrics>()));
    }

    private static RedisException SimulatedRedisFailure() => new("Simulated Redis failure.");
}
