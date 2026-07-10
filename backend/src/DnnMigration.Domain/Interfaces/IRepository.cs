namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Generic asynchronous repository contract exposing standard CRUD operations for a domain entity.
/// Implemented in <c>DnnMigration.Infrastructure</c> over EF Core (<c>DnnDbContext</c>) and consumed
/// by <c>DnnMigration.Application</c> services via constructor dependency injection.
/// </summary>
/// <typeparam name="T">The domain entity type managed by this repository.</typeparam>
// MIGRATION: Generalizes the legacy VB.NET DotNetNuke.Data.DataProvider generic ADO.NET surface
// (Library/Components/Providers/Data/DataProvider.vb, L58-L62: ExecuteNonQuery; ExecuteReader ->
// IDataReader; ExecuteScalar -> Object; ExecuteScalar(Of T); ExecuteDataSet -> DataSet) together with
// the repeating per-entity Get/Add/Update/Delete stored-procedure pattern, into a strongly-typed,
// async, entity-returning CRUD contract. The legacy IDataReader/DataSet/Object return types are
// intentionally eliminated to keep the Domain layer dependency-free; concrete data access (EF Core
// LINQ, AsNoTracking() on read paths) is implemented downstream in DnnMigration.Infrastructure.
public interface IRepository<T> where T : class
{
    /// <summary>Retrieves a single entity by its integer primary key, or <c>null</c> if not found.</summary>
    // MIGRATION: legacy per-entity Get<Entity>(id) stored-proc readers (e.g. DataProvider.GetPortal /
    // GetModule / GetTab returning IDataReader) -> async single-entity lookup.
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all entities of type <typeparamref name="T"/>.</summary>
    // MIGRATION: legacy Get<Entities>() stored-proc readers (e.g. DataProvider.GetPortals / GetAllTabs)
    // -> async materialized collection.
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a new entity and returns the stored instance.</summary>
    // MIGRATION: legacy Add<Entity>(...) stored procs returning the new Integer id (e.g.
    // DataProvider.AddPortalInfo / AddModule) -> async insert returning the persisted entity.
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing entity.</summary>
    // MIGRATION: legacy Update<Entity>(...) stored procs (e.g. DataProvider.UpdatePortalInfo /
    // UpdateModule) -> async update.
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Deletes the entity identified by <paramref name="id"/>.</summary>
    // MIGRATION: legacy Delete<Entity>(id) stored procs (e.g. DataProvider.DeletePortalInfo /
    // DeleteModule) -> async delete by id.
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
