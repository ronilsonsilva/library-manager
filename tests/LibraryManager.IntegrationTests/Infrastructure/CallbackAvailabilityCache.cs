using LibraryManager.Application.Abstractions;
using LibraryManager.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Infrastructure;

internal sealed class CallbackAvailabilityCache(IAvailabilityCache inner) : IAvailabilityCache
{
    public int RemainingRemoveFailures { get; set; }

    public bool FailAllRemoves { get; set; }

    public Guid? FailRemoveForBookId { get; set; }

    public Func<Guid, CancellationToken, Task>? OnRemove { get; set; }

    public Task<BookAvailabilityCacheItem?> GetAsync(Guid bookId, CancellationToken cancellationToken) =>
        inner.GetAsync(bookId, cancellationToken);

    public Task SetAsync(BookAvailabilityCacheItem item, CancellationToken cancellationToken) =>
        inner.SetAsync(item, cancellationToken);

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

            throw new InvalidOperationException("Simulated Redis failure.");
        }

        await inner.RemoveAsync(bookId, cancellationToken);
    }

    public void Reset()
    {
        RemainingRemoveFailures = 0;
        FailAllRemoves = false;
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
            var cache = new CallbackAvailabilityCache(
                new RedisAvailabilityCache(sp.GetRequiredService<IConfiguration>()));
            configure?.Invoke(cache);
            return cache;
        });
        services.AddSingleton<IAvailabilityCache>(sp => sp.GetRequiredService<CallbackAvailabilityCache>());
    }
}
