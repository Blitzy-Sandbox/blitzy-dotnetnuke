using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the Role aggregate, including user-role membership.
/// MIGRATION: ported from the public business surface of RoleController.vb, re-expressed as
/// async DTO-based operations that parallel IRoleRepository. User-role assignment round-trips the
/// full legacy membership metadata (EffectiveDate/ExpiryDate/IsTrialUsed/Subscribed) via
/// <see cref="AssignUserRoleDto"/> (command) and <see cref="UserRoleAssignmentDto"/> (read),
/// preserving parity with the legacy SecurityRoles.ascx.vb "Add Role To User" flow;
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

    // MIGRATION: assignment round-trips the legacy UserRoleInfo membership metadata
    // (EffectiveDate/ExpiryDate/IsTrialUsed/Subscribed) via AssignUserRoleDto rather than only
    // (userId, roleId); the created assignment (including its server-assigned UserRoleID) is
    // returned as a UserRoleAssignmentDto so the caller sees the persisted state.
    Task<UserRoleAssignmentDto> AddUserRoleAsync(AssignUserRoleDto request, CancellationToken cancellationToken = default);

    // Role definitions (catalog) a user currently holds.
    Task<IEnumerable<RoleDto>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default);

    // MIGRATION: read side of the round-trip — the per-assignment membership metadata for a user
    // (effective/expiry window, trial usage, subscription) that the legacy admin screen exposed.
    Task<IEnumerable<UserRoleAssignmentDto>> GetUserRoleAssignmentsAsync(int userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<UserDto>> GetUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default);

    Task RemoveUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default);
}
