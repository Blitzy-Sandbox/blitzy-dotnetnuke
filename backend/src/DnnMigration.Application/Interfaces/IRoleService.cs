using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the Role aggregate, including user-role membership.
/// MIGRATION: ported from the public business surface of RoleController.vb, re-expressed as
/// async DTO-based operations that parallel IRoleRepository. Membership methods use scalar ids
/// and project to RoleDto/UserDto (no UserRoleDto exists). GetRoleGroupsAsync is intentionally
/// omitted (no RoleGroupDto in scope per AAP; returning the RoleGroup entity would leak the domain
/// model). Role is hard-deleted. Implemented by Application/Services/RoleService.cs.
/// </summary>
public interface IRoleService
{
    Task<RoleDto?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);

    Task<IEnumerable<RoleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    Task<RoleDto> CreateAsync(CreateRoleDto request, CancellationToken cancellationToken = default);

    Task<RoleDto> UpdateAsync(UpdateRoleDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds (or refreshes) a user's membership of a role.
    /// MIGRATION (DEV-069): <paramref name="requestedEffectiveDate"/>/<paramref name="requestedExpiryDate"/>
    /// restore the legacy SecurityRoles admin workflow — when EITHER is supplied the dates are used DIRECTLY
    /// (legacy RoleController.AddUserRole with explicit dates); when BOTH are null the legacy
    /// RoleController.UpdateUserRole trial/billing expiry schedule is computed. <paramref name="notify"/> is a
    /// documented NO-OP (the bulk-email subsystem is out of scope per AAP 0.2.2). The 2-argument call shape is
    /// preserved via defaults so existing subscription-style call sites are unaffected.
    /// </summary>
    Task AddUserRoleAsync(
        int userId,
        int roleId,
        DateTime? requestedEffectiveDate = null,
        DateTime? requestedExpiryDate = null,
        bool notify = false,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<RoleDto>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<UserDto>> GetUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default);

    Task RemoveUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default);
}
