namespace AleaSim.Domain.Interfaces;

public interface ITransaction : IDisposable {
    void Commit();
    void Rollback();
}
