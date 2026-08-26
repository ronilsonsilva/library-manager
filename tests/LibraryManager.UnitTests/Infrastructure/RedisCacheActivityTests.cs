using System.Diagnostics;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Telemetry;
using LibraryManager.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;

namespace LibraryManager.UnitTests.Infrastructure;

public sealed class RedisCacheActivityTests
{
    [Fact]
    public async Task GetAsync_starts_library_manager_availability_cache_get_activity()
    {
        using var listener = ListenForLibraryManagerActivities(out var started);
        var cache = CreateCache();

        try
        {
            await cache.GetAsync(Guid.NewGuid(), CancellationToken.None);
        }
        catch (Exception)
        {
            // Redis is unavailable in this unit test; the activity must still start.
        }

        Assert.Contains("availability_cache.get", started);
    }

    [Fact]
    public async Task SetAsync_starts_library_manager_availability_cache_set_activity()
    {
        using var listener = ListenForLibraryManagerActivities(out var started);
        var cache = CreateCache();
        var item = new BookAvailabilityCacheItem(Guid.NewGuid(), 1, 1, true);

        try
        {
            await cache.SetAsync(item, CancellationToken.None);
        }
        catch (Exception)
        {
            // Redis is unavailable in this unit test; the activity must still start.
        }

        Assert.Contains("availability_cache.set", started);
    }

    [Fact]
    public async Task RemoveAsync_starts_library_manager_availability_cache_remove_activity()
    {
        using var listener = ListenForLibraryManagerActivities(out var started);
        var cache = CreateCache();

        try
        {
            await cache.RemoveAsync(Guid.NewGuid(), CancellationToken.None);
        }
        catch (Exception)
        {
            // Redis is unavailable in this unit test; the activity must still start.
        }

        Assert.Contains("availability_cache.remove", started);
    }

    private static ActivityListener ListenForLibraryManagerActivities(out List<string> started)
    {
        var names = new List<string>();
        started = names;
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == LibraryManagerInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => names.Add(activity.OperationName)
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static RedisAvailabilityCache CreateCache()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:1,connectTimeout=250,abortConnect=true"
            })
            .Build();

        return new RedisAvailabilityCache(configuration);
    }
}
