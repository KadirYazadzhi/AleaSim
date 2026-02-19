using AleaSim.Domain.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public interface IRedisCacheService {
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<T?> GetAsync<T>(string key);
    Task RemoveAsync(string key);
    Task<bool> IncrementRateLimitAsync(string key, TimeSpan window);
}

public class RedisCacheService : IRedisCacheService {
    private readonly IDatabase _db;

    public RedisCacheService(IRedisService redisService) {
        _db = redisService.GetDatabase();
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) {
        var json = JsonSerializer.Serialize(value);
        if (expiry.HasValue) {
            await _db.StringSetAsync(key, json, expiry.Value);
        } else {
            await _db.StringSetAsync(key, json);
        }
    }

    public async Task<T?> GetAsync<T>(string key) {
        var value = await _db.StringGetAsync(key);
        if (!value.HasValue) return default;
        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task RemoveAsync(string key) {
        await _db.KeyDeleteAsync(key);
    }

    public async Task<bool> IncrementRateLimitAsync(string key, TimeSpan window) {
        // Atomic increment and expiry setting
        long count = await _db.StringIncrementAsync(key);
        if (count == 1) {
            await _db.KeyExpireAsync(key, window);
        }
        return count > 5; // Allow max 5 requests per window (e.g. 1 second)
    }
}
