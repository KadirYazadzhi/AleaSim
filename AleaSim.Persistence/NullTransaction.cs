using AleaSim.Domain.Interfaces;

namespace AleaSim.Persistence;

public class NullTransaction : ITransaction {
    public void Commit() {
        // Do nothing as this is part of an outer transaction
    }

    public void Rollback() {
        // Do nothing; the outer transaction will handle rollback if it catches the exception
    }

    public void Dispose() {
        // Do nothing
    }
}
