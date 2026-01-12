namespace AleaSim.Domain.Interfaces;

public interface ILockService {
    Task<IDisposable> AcquireLockAsync(string key, TimeSpan timeout);
}
