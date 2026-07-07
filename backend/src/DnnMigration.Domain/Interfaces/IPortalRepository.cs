using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository contract for the <see cref="Portal"/> aggregate. Extends the generic
/// <see cref="IRepository{T}"/> CRUD surface with portal-specific lookups.
/// </summary>
public interface IPortalRepository : IRepository<Portal>
{
    /// <summary>Retrieves the portal matching an HTTP alias, or <c>null</c> if none matches.</summary>
    // MIGRATION: legacy DataProvider.GetPortalByAlias(PortalAlias) [DataProvider.vb L98] returned an
    // IDataReader that PortalController hydrated to a PortalInfo; converted to an async single-entity
    // lookup. Implemented downstream as a LINQ query joining PortalAlias -> Portal in
    // DnnMigration.Infrastructure (no stored proc).
    Task<Portal?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default);
}
