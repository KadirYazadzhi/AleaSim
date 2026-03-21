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
        var db = _redis.GetDatabase();
        string fullKey = LockNamespace + key;
        string lockValue = Guid.NewGuid().ToString();
        
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout) {
            try {
                if (await db.StringSetAsync(fullKey, lockValue, TimeSpan.FromSeconds(30), When.NotExists)) {
                    return new RedisLockReleaser(db, fullKey, lockValue, _logger);
                }
            } catch (RedisException ex) {
                _logger.LogError(ex, "Redis Lock acquisition failed for key {Key}", key);
                // SECURITY: Do NOT fallback to in-memory lock in production
                // Multi-instance deployments require distributed locking
                throw new InvalidOperationException($"Failed to acquire distributed lock for {key}. Redis may be unavailable.", ex);
            }
            await Task.Delay(50);
        }
        
        _logger.LogWarning("Lock acquisition timeout for key {Key} after {Timeout}ms", key, timeout.TotalMilliseconds);
        throw new TimeoutException($"Could not acquire lock for {key} within {timeout.TotalMilliseconds}ms");
    }

    private class RedisLockReleaser : IDisposable {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _value;
        private readonly ILogger _logger;
        private bool _disposed;

        public RedisLockReleaser(IDatabase db, string key, string value, ILogger logger) {
            _db = db;
            _key = key;
            _value = value;
            _logger = logger;
        }

        public void Dispose() {
            if (_disposed) return;
            
            try {
                // Only delete if the value matches (prevents releasing someone else's lock if ours expired)
                // Using a Lua script for atomicity
                string luaScript = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
                var result = _db.ScriptEvaluate(luaScript, new RedisKey[] { _key }, new RedisValue[] { _value });
                
                if (result.IsNull || (int)result == 0) {
                    _logger.LogWarning("Lock {Key} was already released or expired", _key);
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to release Redis lock {Key}", _key);
            }
            
            _disposed = true;
        }
    }
}
