using AleaSim.Domain.Interfaces;
using StackExchange.Redis;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AleaSim.Domain.Services;

public class RedisLockService : ILockService {
    private readonly IRedisService _redis;
    private readonly ILogger<RedisLockService> _logger;
    private readonly InMemoryLockService _fallback;
    private static readonly string LockNamespace = "aleasim:lock:";

    public RedisLockService(IRedisService redis, ILogger<RedisLockService> logger) {
        _redis = redis;
        _logger = logger;
        _fallback = new InMemoryLockService(); // Local backup
    }

    public async Task<IDisposable> AcquireLockAsync(string key, TimeSpan timeout) {
        try {
            var db = _redis.GetDatabase();
            string fullKey = LockNamespace + key;
            string lockValue = Guid.NewGuid().ToString();
            
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout) {
                if (await db.StringSetAsync(fullKey, lockValue, TimeSpan.FromSeconds(30), When.NotExists)) {
                    return new RedisLockReleaser(db, fullKey, lockValue);
                }
                await Task.Delay(50);
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Redis Lock failed, falling back to In-Memory lock.");
        }

        // Fallback to local memory lock (accurate for single-server, prevents crash in cluster)
        return await _fallback.AcquireLockAsync(key, timeout);
    }

    private class RedisLockReleaser : IDisposable {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _value;
        private bool _disposed;

        public RedisLockReleaser(IDatabase db, string key, string value) {
            _db = db;
            _key = key;
            _value = value;
        }

        public void Dispose() {
            if (_disposed) return;
            
            // Only delete if the value matches (prevents releasing someone else's lock if ours expired)
            // Using a Lua script for atomicity
            string luaScript = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
            _db.ScriptEvaluate(luaScript, new RedisKey[] { _key }, new RedisValue[] { _value });
            
            _disposed = true;
        }
    }
}
