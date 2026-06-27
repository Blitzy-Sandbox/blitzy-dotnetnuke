using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Service-layer contract for role management. Consumed by RolesController via constructor
/// injection. The implementation orchestrates the role repository + unit of work and projects
/// Domain entities to DTOs. User-role assignment (assign/remove/update) is supported via AssignUserRoleAsync/RemoveUserRoleAsync/UpdateUserRoleAsync plus the read-only GetUserRolesAsync lookup.
/// </summary>
// MIGRATION: Abstracted from the public operations of Library/Components/Security/Roles/RoleController.vb
// (AddRole L100, UpdateRole L254, DeleteRole L125, GetRole L163, GetPortalRoles L146, GetUserRoles L392).
// Business logic moves to RoleService; data access to IRoleRepository. DTO-only contract (AAP 0.7.7).
//
// MIGRATION (SCOPE - user-role WRITE IMPLEMENTED): The legacy AddUserRole (L277/L295), DeleteUserRole (L330) and
// UpdateUserRole (L472/L489) write operations are now declared on this interface and implemented in RoleService,
// completing role/permission management workflow parity (AAP 0.7.1). The enabling pieces are now present:
// (1) IRoleRepository exposes GetUserRoleAsync/AddUserRoleAsync/UpdateUserRoleAsync/RemoveUserRoleAsync;
// (2) the AssignUserRoleRequest/UpdateUserRoleRequest DTOs exist; (3) the assignment sub-resource is exposed at
// POST/PUT /api/roles/assignments and DELETE /api/roles/{roleId}/users/{userId}. Only RoleGroup CRUD (out of the
// AAP 0.3.4 Roles resource surface) and SendNotification email (DNN Mail excluded by AAP 0.6.2) remain out of scope.
public interface IRoleService
{
    // MIGRATION: Legacy GetPortalRoles (RoleController.vb L146) — scoped by portalId (multi-tenant, AAP 0.7.1), paged.
    Task<Result<PagedResult<RoleResponse>>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy GetRole(RoleID, PortalID) (RoleController.vb L163). CP1 review (IRoleService #1) — PORTAL-SCOPED:
    // portalId is a CONTRACT parameter so tenant ownership is enforceable at the Application boundary (AAP 0.7.1).
    Task<Result<RoleResponse>> GetByIdAsync(int portalId, int roleId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy AddRole (RoleController.vb L100). POST /api/roles -> 201. portalId is carried inside the request
    // (CreateRoleRequest.PortalId).
    Task<Result<RoleResponse>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy UpdateRole (RoleController.vb L254). CP1 review (IRoleService #1) — PORTAL-SCOPED: portalId +
    // roleId identify the target so the update is constrained to the owning portal and the system-role guard can read
    // the portal's Administrator/Registered role ids. PUT -> 200.
    Task<Result<RoleResponse>> UpdateAsync(int portalId, int roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy DeleteRole(RoleId, PortalId) (RoleController.vb L125). CP1 review (IRoleService #1) — PORTAL-SCOPED.
    // DELETE -> 204. Non-generic Result.
    Task<Result> DeleteAsync(int portalId, int roleId, CancellationToken cancellationToken = default);

    // MIGRATION: READ-ONLY projection of legacy GetUserRoles(PortalId, UserId) (RoleController.vb L392), backed by
    // IRoleRepository.GetUserRolesAsync(int portalId, int userId) and flattened to UserRoleDto. CP1 review (IRoleService #1)
    // — PORTAL-SCOPED (the legacy query carried PortalId). Assignment WRITE is supported via AssignUserRoleAsync/RemoveUserRoleAsync/UpdateUserRoleAsync (see those methods).
    Task<Result<IEnumerable<UserRoleDto>>> GetUserRolesAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy AddUserRole (RoleController.vb L277/L295). Assigns a user to a role (upsert: insert when the
    // user does not yet hold the role, else refresh the effective/expiry dates). POST /api/roles/assignments -> 201.
    Task<Result<UserRoleDto>> AssignUserRoleAsync(AssignUserRoleRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy DeleteUserRole (RoleController.vb L330) + CanRemoveUserFromRole guard (L741/L764). Removes a
    // user from a role unless the guard blocks it (the portal Administrator cannot be removed from the Administrators
    // role, and NO user can be removed from the Registered Users role). DELETE -> 204; a blocked removal -> 400.
    Task<Result> RemoveUserRoleAsync(int portalId, int userId, int roleId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy UpdateUserRole (RoleController.vb L472/L489). Recomputes the assignment's expiry from the role's
    // trial/billing schedule (N/O/D/W/M/Y), or on Cancel expires (paid + trial-used) / removes it. PUT -> 200.
    Task<Result<UserRoleDto>> UpdateUserRoleAsync(UpdateUserRoleRequest request, CancellationToken cancellationToken = default);
}
