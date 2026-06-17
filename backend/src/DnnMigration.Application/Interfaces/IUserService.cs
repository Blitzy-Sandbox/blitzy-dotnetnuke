using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the User aggregate (record management only; authentication
/// lives in IAuthService). MIGRATION: ported from the public business surface of UserController.vb,
/// re-expressed as async DTO-based operations that parallel IUserRepository. User is HARD-deleted
/// (the DNN 4.9 dbo.Users table has no IsDeleted column; see MIGRATION_NOTES.md §6.3 / D-014).
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

    /// <summary>
    /// Sets (or clears) the "force password change on next login" requirement for a user, persisting the
    /// real <c>dbo.Users.UpdatePassword</c> column, and returns the updated user projection.
    /// </summary>
    /// <remarks>
    /// MIGRATION (CP-FINAL / Code-Review G5): reproduces the legacy DNN Membership admin workflow
    /// (UserMembership.UpdatePassword toggled from Membership.ascx.vb). Unlike Unlock/Approved — which live on
    /// the GUID-keyed aspnet_Membership table that ADR-002 forbids adding to the model — UpdatePassword is a
    /// physical dbo.Users column, so this transition is fully implementable and persisted.
    /// </remarks>
    /// <param name="userId">The id of the user whose flag is being set.</param>
    /// <param name="require">
    /// <see langword="true"/> to require a password change at next login; <see langword="false"/> to clear it.
    /// </param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The updated <see cref="UserDto"/> reflecting the new <c>UpdatePassword</c> value.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the target user does not exist.</exception>
    Task<UserDto> SetForcePasswordChangeAsync(int userId, bool require, CancellationToken cancellationToken = default);

    Task DeleteAsync(int userId, CancellationToken cancellationToken = default);
}
