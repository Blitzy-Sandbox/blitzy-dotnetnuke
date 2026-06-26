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
    // (host-level listing); IDataReader -> async LINQ over Portal entities. Retained for internal all-portal reads
    // (e.g. PortalService.DeleteAsync, which enumerates the portal's users); the host LIST endpoint uses the paged
    // overload below to avoid over-fetching.
    Task<IEnumerable<Portal>> GetAllAsync();

    // MIGRATION: CP1 review (PortalService #5 / performance #22 — avoid over-fetching, AAP 0.7.7). The legacy host
    // portal grid paged at the data source (PortalController.GetPortalsByName(..., pageIndex, pageSize, ByRef total)
    // L262), NOT by fetching every portal and slicing in memory. This paged query returns ONLY the requested page plus
    // the total count, replacing the previous fetch-all-then-Skip/Take-in-memory in PortalService.GetAllAsync.
    // PageIndex is ZERO-BASED (see PagedResult) for behavioral parity.
    Task<(IEnumerable<Portal> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize);

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

    // MIGRATION: Replaces PortalController.GetPortalSpaceUsedBytes(portalId) L1296 -> DataProvider.GetPortalSpaceUsed —
    // the legacy storage-quota numerator that read the persisted "SpaceUsed" column (Convert.ToInt64, 0 when DBNull or
    // no row). CP1 review (PortalService #4 CRITICAL): the quota check MUST use real consumed bytes, not a hardcoded 0.
    // Returns the total bytes consumed by the portal (0 when the portal has no usage row), in bytes (Long -> long).
    Task<long> GetSpaceUsedBytesAsync(int portalId);
}
