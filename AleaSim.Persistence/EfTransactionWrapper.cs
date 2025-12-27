using AleaSim.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace AleaSim.Persistence;

public class EfTransactionWrapper : ITransaction {
    private readonly IDbContextTransaction _transaction;

    public EfTransactionWrapper(IDbContextTransaction transaction) {
        _transaction = transaction;
    }

    public void Commit() {
        _transaction.Commit();
    }

    public void Rollback() {
        _transaction.Rollback();
    }

    public void Dispose() {
        _transaction.Dispose();
    }
}
