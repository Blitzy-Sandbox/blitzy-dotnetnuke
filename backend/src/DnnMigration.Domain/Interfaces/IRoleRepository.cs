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
    // multi-tenant isolation (AAP 0.7.1).
    Task<IEnumerable<Role>> GetByPortalIdAsync(int portalId);

    // MIGRATION: Replaces RoleController.GetRole/GetRoleById. Returns null when not found.
    Task<Role?> GetByIdAsync(int roleId);

    // MIGRATION: Replaces RoleController.AddRole. Returns the persisted Role so the database-generated RoleId is available.
    Task<Role> AddAsync(Role role);

    // MIGRATION: Replaces RoleController.UpdateRole.
    Task UpdateAsync(Role role);

    // MIGRATION: Replaces RoleController.DeleteRole.
    Task DeleteAsync(int roleId);

    // MIGRATION: Replaces RoleController.GetUserRoles(PortalId, UserId) (returned ArrayList of UserRoleInfo).
    // Collapsed to a userId-only lookup (a user already belongs to a portal). Returns the UserRole join entities
    // (UserId, RoleId, EffectiveDate, ExpiryDate, ...) that model the many-to-many membership, preserving the
    // user -> role association (AAP 0.7.1).
    Task<IEnumerable<UserRole>> GetUserRolesAsync(int userId);
}
