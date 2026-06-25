namespace DnnMigration.Domain.Interfaces;

// MIGRATION: Replaces the ADO.NET transaction surface of the legacy abstract DataProvider
// (Library/Components/Providers/Data/DataProvider.vb: GetTransaction()/CommitTransaction(DbTransaction)/
// RollbackTransaction(DbTransaction)). In the legacy reflection-instantiated provider, controllers passed a
// System.Data.Common.DbTransaction around manual ExecuteNonQuery/ExecuteReader calls. That surface is replaced
// by the EF Core 8 Unit-of-Work boundary: SaveChangesAsync wraps a transactional unit, and an explicit
// Begin/Commit/Rollback span coordinates multiple SaveChanges calls. The concrete implementation (wrapping
// DnnDbContext.SaveChangesAsync and IDbContextTransaction) lives in DnnMigration.Infrastructure/Repositories.
public interface IUnitOfWork
{
    // MIGRATION: Replaces the implicit per-stored-procedure commit of the legacy provider with an explicit,
    // async persistence boundary. Returns the number of state entries written to the database.
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // MIGRATION: Replaces DataProvider.GetTransaction() (which returned a DbTransaction). The EF Core
    // implementation begins an IDbContextTransaction on the underlying DnnDbContext.
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    // MIGRATION: Replaces DataProvider.CommitTransaction(DbTransaction). Commits the ambient transaction
    // started by BeginTransactionAsync (the implementation owns the transaction handle, so no parameter is needed).
    Task CommitAsync(CancellationToken cancellationToken = default);

    // MIGRATION: Replaces DataProvider.RollbackTransaction(DbTransaction). Rolls back the ambient transaction.
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
