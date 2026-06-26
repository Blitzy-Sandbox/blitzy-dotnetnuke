using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

// MIGRATION: Abstracts the role data-access surface of the legacy RoleController.vb (DNN 4.x persisted roles via
// a separate RoleProvider, NOT the DataProvider). Replaces the ArrayList/IDataReader pipeline with async LINQ
// returning Role / UserRole entities. Concrete EF Core implementation lives in
// DnnMigration.Infrastructure/Repositories/RoleRepository.cs. Roles are portal-scoped (multi-tenant, AAP 0.7.1)
// and preserve the user -> role -> permission model.
public interface IRoleRepository
{
    // MIGRATION: Replaces RoleController.GetPortalRoles(PortalId) (returned ArrayList). Portal-scoped for
    // multi-tenant isolation (AAP 0.7.1). Retained for internal all-portal reads (the create-time duplicate-name scan
    // and AutoAssignUsers); list endpoints use the paged overload below to avoid over-fetching.
    Task<IEnumerable<Role>> GetByPortalIdAsync(int portalId);

    // MIGRATION: CP1 review (performance #22 — avoid over-fetching, AAP 0.7.7) — paged portal-scoped query returning
    // ONLY the requested page plus the total count, replacing the previous fetch-all-then-page-in-memory in
    // RoleService.GetByPortalAsync. Mirrors the legacy GetPortalRoles paging (zero-based page index preserved).
    Task<(IEnumerable<Role> Items, int TotalCount)> GetByPortalPagedAsync(int portalId, int pageIndex, int pageSize);

    // MIGRATION: Replaces RoleController.GetRole(RoleID, PortalID). PORTAL-SCOPED (CP1 review IRoleRepository #1 /
    // multi-tenant isolation, AAP 0.7.1): the lookup MUST carry portalId so the service can enforce that a role is only
    // read/mutated within its owning portal. Returns null when no role with that id exists IN that portal.
    Task<Role?> GetByIdAsync(int portalId, int roleId);

    // MIGRATION: Replaces RoleController.AddRole. Returns the persisted Role so the database-generated RoleId is available.
    Task<Role> AddAsync(Role role);

    // MIGRATION: Replaces RoleController.UpdateRole.
    Task UpdateAsync(Role role);

    // MIGRATION: Replaces RoleController.DeleteRole(RoleId, PortalId). PORTAL-SCOPED (CP1 review IRoleRepository #1):
    // carries portalId so a delete is constrained to the owning portal (the roleId-only collapse is rejected — it
    // cannot enforce the legacy portal-scoped delete).
    Task DeleteAsync(int portalId, int roleId);

    // MIGRATION: Replaces RoleController.GetUserRoles(PortalId, UserId) (returned ArrayList of UserRoleInfo).
    // PORTAL-SCOPED (CP1 review IRoleRepository #1): the legacy signature carried PortalId; the earlier userId-only
    // collapse is rejected by the review because it cannot express the legacy portal-scoped query. Returns the UserRole
    // join entities (UserId, RoleId, EffectiveDate, ExpiryDate, ...) that model the many-to-many membership, preserving
    // the user -> role association within the portal (AAP 0.7.1).
    Task<IEnumerable<UserRole>> GetUserRolesAsync(int portalId, int userId);
}
