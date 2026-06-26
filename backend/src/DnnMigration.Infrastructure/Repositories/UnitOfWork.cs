using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DnnMigration.Infrastructure.Repositories;

// MIGRATION: Replaces DotNetNuke.Data.DataProvider transaction members (DataProvider.vb: GetTransaction L73,
// CommitTransaction L71, RollbackTransaction L74). The legacy reflection singleton (Instance() L48) is replaced by
// request-scoped DI; the EF Core DnnDbContext is the unit of work and SaveChangesAsync is the implicit commit.
//
// In the legacy provider, controllers obtained a System.Data.Common.DbTransaction via GetTransaction(), threaded it
// through manual Execute* stored-procedure calls, then called CommitTransaction/RollbackTransaction explicitly. Here
// the repositories only STAGE changes (Add/Update/Remove) against the shared, request-scoped DnnDbContext, and the
// Application service decides the persistence boundary by calling SaveChangesAsync (single atomic write) or by
// wrapping multiple SaveChanges in an explicit BeginTransactionAsync/CommitAsync/RollbackAsync span.
public sealed class UnitOfWork : IUnitOfWork
{
    // MIGRATION: The single, request-scoped DnnDbContext is THE unit of work. It is constructor-injected (not built
    // by reflection like the legacy DataProvider.CreateProvider() L44) and is the SAME instance shared with every
    // repository in the current request, so all staged changes commit together in one SaveChangesAsync call.
    private readonly DnnDbContext _context;

    // Holds the active explicit transaction between BeginTransactionAsync and Commit/Rollback; null when no explicit
    // transaction is in flight (e.g. the common single-SaveChanges path, or under a non-relational provider).
    private IDbContextTransaction? _transaction;

    public UnitOfWork(DnnDbContext context) => _context = context;

    // MIGRATION: Replaces the legacy implicit "commit" where each DataProvider.Execute* call ran its own stored
    // procedure and committed independently. Persists ALL staged changes in one atomic round-trip and returns the
    // number of state entries written. This is NOT guarded by IsRelational(): SaveChangesAsync is supported by every
    // EF Core provider - including Microsoft.EntityFrameworkCore.InMemory used by the unit tests (Gate 2) and
    // integration tests (Gate 5) - so it must remain the universal commit path. Returns the Task directly (no await).
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: Replaces DataProvider.GetTransaction() (L73), which returned a raw DbTransaction. Begins an EF Core
    // IDbContextTransaction on the underlying DnnDbContext and retains the handle so Commit/Rollback need no parameter.
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // MIGRATION (relational guard): the Microsoft.EntityFrameworkCore.InMemory provider used by the Gate 2/Gate 5
        // test suites does NOT support transactions and throws on BeginTransactionAsync. IsRelational() returns false
        // under InMemory and true under the SqlServer provider, so this guard lets InMemory-backed tests run the same
        // service code paths without throwing, while real (relational) execution still opens a database transaction.
        if (!_context.Database.IsRelational())
        {
            return;
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    // MIGRATION: Replaces DataProvider.CommitTransaction(DbTransaction) (L71). Flushes any pending changes, commits the
    // ambient transaction opened by BeginTransactionAsync, then disposes and clears the handle. `is not null` is the
    // C# translation of the legacy VB `IsNot Nothing` guard.
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            return;
        }

        if (_transaction is not null)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    // MIGRATION: Replaces DataProvider.RollbackTransaction(DbTransaction) (L74). Rolls back the ambient transaction,
    // then disposes and clears the handle - symmetric to CommitAsync - so a disposed transaction is never reused.
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            return;
        }

        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
