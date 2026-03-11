using AleaSim.Domain.Interfaces;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AleaSim.Domain.Services;

public interface IRedisCacheService {
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<T?> GetAsync<T>(string key);
    Task RemoveAsync(string key);
    Task<bool> IncrementRateLimitAsync(string key, TimeSpan window, int maxRequests = 5);
}

public class RedisCacheService : IRedisCacheService {
    private readonly IDatabase? _db;
    private readonly IMemoryCache _localCache;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheService(IRedisService redisService, IMemoryCache localCache, ILogger<RedisCacheService> logger) {
        try {
            _db = redisService.GetDatabase();
        } catch {
            _db = null;
        }
        _localCache = localCache;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) {
        try {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            if (_db != null) {
                if (expiry.HasValue) await _db.StringSetAsync(key, json, expiry.Value);
                else await _db.StringSetAsync(key, json);
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Redis Set failed, falling back to local memory.");
        }
        
        // Always set local as secondary/fallback
        if (expiry.HasValue) _localCache.Set(key, value, expiry.Value);
        else _localCache.Set(key, value, TimeSpan.FromMinutes(10));
    }

    public async Task<T?> GetAsync<T>(string key) {
        try {
            if (_db != null) {
                var value = await _db.StringGetAsync(key);
                if (value.HasValue) return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Redis Get failed, checking local memory.");
        }

        return _localCache.Get<T>(key);
    }

    public async Task RemoveAsync(string key) {
        try {
            if (_db != null) await _db.KeyDeleteAsync(key);
        } catch { }
        _localCache.Remove(key);
    }

    public async Task<bool> IncrementRateLimitAsync(string key, TimeSpan window, int maxRequests = 5) {
        try {
            if (_db != null) {
                long count = await _db.StringIncrementAsync(key);
                if (count == 1) await _db.KeyExpireAsync(key, window);
                return count > maxRequests;
            }
        } catch { }

        // Local fallback for rate limiting (less accurate in cluster but prevents crash)
        int localCount = _localCache.Get<int?>(key) ?? 0;
        localCount++;
        _localCache.Set(key, localCount, window);
        return localCount > maxRequests;
    }
}
