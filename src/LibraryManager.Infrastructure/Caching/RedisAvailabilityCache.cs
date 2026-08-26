using System.Text.Json;
using LibraryManager.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace LibraryManager.Infrastructure.Caching;

public sealed class RedisAvailabilityCache : IAvailabilityCache
{
    public const int TimeToLiveSeconds = 60;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly Lazy<IConnectionMultiplexer> _redis;

    public RedisAvailabilityCache(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        _redis = new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(connectionString));
    }

    public async Task<BookAvailabilityCacheItem?> GetAsync(Guid bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _redis.Value.GetDatabase().StringGetAsync(Key(bookId));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<BookAvailabilityCacheItem>((string)value!, JsonOptions);
    }

    public async Task SetAsync(BookAvailabilityCacheItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(item, JsonOptions);
        await _redis.Value.GetDatabase().StringSetAsync(
            Key(item.BookId),
            json,
            TimeSpan.FromSeconds(TimeToLiveSeconds));
    }

    public async Task RemoveAsync(Guid bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redis.Value.GetDatabase().KeyDeleteAsync(Key(bookId));
    }

    public static string Key(Guid bookId) => $"library-manager:books:{bookId}:availability";
}
