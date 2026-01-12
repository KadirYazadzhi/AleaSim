using System.Collections.Concurrent;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class InMemoryLockService : ILockService {
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireLockAsync(string key, TimeSpan timeout) {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(timeout)) {
            throw new TimeoutException($"Could not acquire lock for key: {key}");
        }
        return new LockReleaser(semaphore);
    }

    private class LockReleaser : IDisposable {
        private readonly SemaphoreSlim _semaphore;
        public LockReleaser(SemaphoreSlim semaphore) => _semaphore = semaphore;
        public void Dispose() => _semaphore.Release();
    }
}
