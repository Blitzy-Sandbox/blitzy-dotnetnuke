using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

// MIGRATION: Abstracts the portal data-access surface of the legacy abstract DataProvider
// (Library/Components/Providers/Data/DataProvider.vb) and PortalController.vb. The reflection-instantiated
// DataProvider.Instance() + SqlHelper + stored-procedure + IDataReader/FillInfo pipeline is replaced by a
// constructor-injected repository returning Domain entities. Concrete EF Core implementation lives in
// DnnMigration.Infrastructure/Repositories/PortalRepository.cs. Portal is the multi-tenant root entity.
public interface IPortalRepository
{
    // MIGRATION: Replaces DataProvider.GetPortals()/PortalController.GetPortals (L1263). Returns all portals
    // (host-level listing); IDataReader -> async LINQ over Portal entities.
    Task<IEnumerable<Portal>> GetAllAsync();

    // MIGRATION: Replaces DataProvider.GetPortal(PortalId). Returns null when not found (nullable reference types ON).
    Task<Portal?> GetByIdAsync(int portalId);

    // MIGRATION: Replaces DataProvider.GetPortalByAlias(PortalAlias) — the multi-tenant request-routing lookup
    // that resolves the active portal from an HTTP alias. Returns null when no portal matches the alias.
    Task<Portal?> GetByAliasAsync(string portalAlias);

    // MIGRATION: Collapses the two legacy creation overloads DataProvider.CreatePortal(...) and
    // DataProvider.AddPortalInfo(...) (and PortalController.CreatePortal L980) into a single entity-based AddAsync.
    // Returns the persisted Portal so the database-generated PortalId is available to the caller.
    Task<Portal> AddAsync(Portal portal);

    // MIGRATION: Replaces DataProvider.UpdatePortalInfo(...) / PortalController.UpdatePortalInfo (L1568).
    Task UpdateAsync(Portal portal);

    // MIGRATION: Replaces DataProvider.DeletePortalInfo(PortalId) / PortalController.DeletePortalInfo (L1191).
    Task DeleteAsync(int portalId);
}
