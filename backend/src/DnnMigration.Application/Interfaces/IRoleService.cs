using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Service-layer contract for role management. Consumed by RolesController via constructor
/// injection. The implementation orchestrates the role repository + unit of work and projects
/// Domain entities to DTOs. Role-to-user assignment is read-only in this phase (see migration note).
/// </summary>
// MIGRATION: Abstracted from the public operations of Library/Components/Security/Roles/RoleController.vb
// (AddRole L100, UpdateRole L254, DeleteRole L125, GetRole L163, GetPortalRoles L146, GetUserRoles L392).
// Business logic moves to RoleService; data access to IRoleRepository. DTO-only contract (AAP 0.7.7).
//
// MIGRATION (SCOPE — user-role WRITE deferred): The legacy AddUserRole (L277/L295), DeleteUserRole (L330) and
// UpdateUserRole (L472/L489) write operations are intentionally NOT declared on this interface this phase. The
// enabling pieces are absent: (1) IRoleRepository exposes only GetUserRolesAsync (no user-role write method);
// (2) no AssignUserRoleRequest DTO exists (the DTOs/Role layer deliberately omitted it); (3) AAP 0.3.4 defines the
// Roles resource as CRUD only with no assignment sub-resource. Declaring them would be unimplementable and break
// Gate 1/Gate 2. The user-role surface is therefore exposed READ-ONLY via GetUserRolesAsync.
public interface IRoleService
{
    // MIGRATION: Legacy GetPortalRoles (RoleController.vb L146) — scoped by portalId (multi-tenant, AAP 0.7.1), paged.
    Task<Result<PagedResult<RoleResponse>>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy GetRole (RoleController.vb L163). Single key (roleId) in the contract.
    Task<Result<RoleResponse>> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy AddRole (RoleController.vb L100). POST /api/roles -> 201.
    Task<Result<RoleResponse>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy UpdateRole (RoleController.vb L254). roleId route-bound; PUT -> 200.
    Task<Result<RoleResponse>> UpdateAsync(int roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy DeleteRole(RoleId, PortalId) (RoleController.vb L125). DELETE -> 204. Non-generic Result.
    Task<Result> DeleteAsync(int roleId, CancellationToken cancellationToken = default);

    // MIGRATION: READ-ONLY projection of legacy GetUserRoles(PortalId, UserId) (RoleController.vb L392), backed by
    // IRoleRepository.GetUserRolesAsync(int userId) and flattened to UserRoleDto. Assignment WRITE is deferred
    // (see the interface-level scope note above).
    Task<Result<IEnumerable<UserRoleDto>>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default);
}
