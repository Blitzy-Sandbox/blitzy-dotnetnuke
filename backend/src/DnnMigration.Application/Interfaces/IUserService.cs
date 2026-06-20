using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the User aggregate (record management only; authentication
/// lives in IAuthService). MIGRATION: ported from the public business surface of UserController.vb,
/// re-expressed as async DTO-based operations that parallel IUserRepository. User delete is a HARD
/// delete (the DNN 4.9 [Users] table has no IsDeleted column, so a soft delete is impossible without a
/// schema change which ADR-002 forbids; DEV-039).
/// Implemented by Application/Services/UserService.cs.
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
}
