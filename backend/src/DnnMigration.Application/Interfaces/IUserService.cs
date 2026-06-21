using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the User aggregate (record management only; authentication
/// lives in IAuthService). MIGRATION: ported from the public business surface of UserController.vb,
/// re-expressed as async DTO-based operations that parallel IUserRepository. User delete is a NON-destructive
/// (soft) delete realized by removing the user's portal membership (the DNN 4.9 [Users] table has no IsDeleted
/// column and ADR-002 forbids adding one; DEV-066). The Membership workflow transitions — authorize /
/// unauthorize / unlock / force-password-change (Website/admin/Users/Membership.ascx.vb) — are exposed here
/// (DEV-067). Implemented by Application/Services/UserService.cs.
/// </summary>
public interface IUserService
{
    Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<UserDto?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default);

    Task<UserDto?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default);

    Task<PagedResult<UserDto>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(UpdateUserDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flags a user so they are required to change their password on next login by setting the mapped
    /// <c>[Users].UpdatePassword</c> column. MIGRATION: reproduces the legacy admin "force password change"
    /// affordance (<c>cmdPassword_Click</c> in <c>Website/admin/Users/Membership.ascx.vb</c>), which set
    /// exactly this flag. Returns the updated user. Throws <see cref="KeyNotFoundException"/> when no user
    /// exists for <paramref name="userId"/>.
    /// </summary>
    Task<UserDto> ForcePasswordChangeAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user's ASP.NET Membership state (approval / lockout / must-change-password). MIGRATION
    /// (DEV-067): the read backing the Membership workflow (<c>Website/admin/Users/Membership.ascx.vb</c>).
    /// Throws <see cref="KeyNotFoundException"/> when no user or membership record exists for
    /// <paramref name="userId"/>.
    /// </summary>
    Task<MembershipDto> GetMembershipAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the user's membership and returns the refreshed state. MIGRATION (DEV-067): the legacy
    /// <c>cmdAuthorize_Click</c> transition. Throws <see cref="KeyNotFoundException"/> when no membership
    /// record exists for <paramref name="userId"/>.
    /// </summary>
    Task<MembershipDto> AuthorizeAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the user's membership approval and returns the refreshed state. MIGRATION (DEV-067): the legacy
    /// <c>cmdUnAuthorize_Click</c> transition. Throws <see cref="KeyNotFoundException"/> when no membership
    /// record exists for <paramref name="userId"/>.
    /// </summary>
    Task<MembershipDto> UnauthorizeAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the user's lockout and returns the refreshed state. MIGRATION (DEV-067): the legacy
    /// <c>cmdUnLock_Click</c> transition. Throws <see cref="KeyNotFoundException"/> when no membership record
    /// exists for <paramref name="userId"/>.
    /// </summary>
    Task<MembershipDto> UnlockAsync(int userId, CancellationToken cancellationToken = default);
}
