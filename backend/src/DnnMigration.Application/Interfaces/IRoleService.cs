using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the Role aggregate, including user-role membership.
/// MIGRATION: ported from the public business surface of RoleController.vb, re-expressed as
/// async DTO-based operations that parallel IRoleRepository. User-role assignment follows the legacy
/// ADMIN path (RoleController.AddUserRole): the <see cref="AssignUserRoleDto"/> command carries only the
/// admin-supplied effective/expiry window (UserID/RoleID/EffectiveDate/ExpiryDate) and the operation
/// UPSERTS it, while the <see cref="UserRoleAssignmentDto"/> read returns the full persisted state
/// (including IsTrialUsed/Subscribed). IsTrialUsed/Subscribed are NOT write inputs — they belong to the
/// out-of-scope self-service UpdateUserRole(Cancel) path (see MIGRATION_NOTES.md §6.2). This preserves
/// parity with the legacy SecurityRoles.ascx.vb "Add Role To User" flow;
/// <see cref="GetUserRolesAsync"/> still projects the role definitions a user holds (RoleDto) and
/// <see cref="GetUsersInRoleAsync"/> the role membership (UserDto). GetRoleGroupsAsync is
/// intentionally omitted (no RoleGroupDto in scope per AAP; returning the RoleGroup entity would
/// leak the domain model). Role is hard-deleted. Implemented by Application/Services/RoleService.cs.
/// </summary>
public interface IRoleService
{
    Task<RoleDto?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);

    Task<IEnumerable<RoleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    Task<RoleDto> CreateAsync(CreateRoleDto request, CancellationToken cancellationToken = default);

    Task<RoleDto> UpdateAsync(UpdateRoleDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int roleId, CancellationToken cancellationToken = default);

    // MIGRATION: ADMIN assignment (RoleController.AddUserRole, EffectiveDate/ExpiryDate overload). Accepts the
    // admin-supplied effective/expiry window via AssignUserRoleDto (UserID/RoleID/EffectiveDate/ExpiryDate) and
    // UPSERTS the assignment (create if absent, else update the existing window). Returns the persisted
    // assignment — including its server-assigned UserRoleID and full stored state (IsTrialUsed/Subscribed) — as a
    // UserRoleAssignmentDto so the caller sees the persisted result. IsTrialUsed/Subscribed are NOT write inputs.
    Task<UserRoleAssignmentDto> AddUserRoleAsync(AssignUserRoleDto request, CancellationToken cancellationToken = default);

    // Role definitions (catalog) a user currently holds.
    Task<IEnumerable<RoleDto>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default);

    // MIGRATION: read side of the round-trip — the per-assignment membership metadata for a user
    // (effective/expiry window, trial usage, subscription) that the legacy admin screen exposed.
    Task<IEnumerable<UserRoleAssignmentDto>> GetUserRoleAssignmentsAsync(int userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<UserDto>> GetUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default);

    Task RemoveUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default);
}
