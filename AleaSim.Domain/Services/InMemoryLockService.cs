using System.Collections.Concurrent;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class InMemoryLockService : ILockService {
    private static readonly ConcurrentDictionary<string, LockEntry> _locks = new();

    private class LockEntry {
        public SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount = 0;
    }

    public async Task<IDisposable> AcquireLockAsync(string key, TimeSpan timeout) {
        var entry = _locks.GetOrAdd(key, _ => new LockEntry());
        Interlocked.Increment(ref entry.RefCount);

        try {
            if (!await entry.Semaphore.WaitAsync(timeout)) {
                Interlocked.Decrement(ref entry.RefCount);
                throw new TimeoutException($"Could not acquire lock for key: {key}");
            }
        }
        catch {
            Interlocked.Decrement(ref entry.RefCount);
            throw;
        }

        return new LockReleaser(key, entry);
    }

    private class LockReleaser : IDisposable {
        private readonly string _key;
        private readonly LockEntry _entry;
        private bool _disposed;

        public LockReleaser(string key, LockEntry entry) {
            _key = key;
            _entry = entry;
        }

        public void Dispose() {
            if (_disposed) return;
            _entry.Semaphore.Release();
            
            if (Interlocked.Decrement(ref _entry.RefCount) == 0) {
                // Try to remove if no one else is using it. 
                // There's a small race condition where someone might GetOrAdd 
                // right after Decrement but before Remove, but RefCount will handle consistency.
                _locks.TryRemove(_key, out _);
            }
            _disposed = true;
        }
    }
}
