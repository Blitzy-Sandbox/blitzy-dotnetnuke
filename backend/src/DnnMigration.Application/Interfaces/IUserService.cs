using DnnMigration.Application.DTOs;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for user management, exposing the CRUD and change-password
/// surface consumed by the <c>/api/users</c> endpoints.
/// </summary>
// MIGRATION: replaces the business surface of the legacy DotNetNuke UserController.vb. The legacy
// Public Shared (static) members (e.g. GetUser L497, GetUsers L685, DeleteUser L200) become
// DI-registered instance methods (AAP static->instance rule). DTO-only, async.
public interface IUserService
{
    /// <summary>Returns all users.</summary>
    Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all users belonging to the specified portal.</summary>
    // MIGRATION: legacy UserController.GetUsers(portalId).
    Task<IEnumerable<UserDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Returns the user with the given id, or <c>null</c> if not found.</summary>
    Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns a user within a portal by username, or <c>null</c> if not found.</summary>
    // MIGRATION: legacy UserController.GetUserByName(portalId, username).
    Task<UserDto?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches users, optionally scoped to a single portal. A field-specific request
    /// (<paramref name="filterProperty"/> = <c>Username</c> or <c>Email</c> plus <paramref name="filter"/>)
    /// restricts matching to that column; otherwise the free-text <paramref name="query"/> is matched
    /// (case-insensitive substring) across username/email/display-name/first-name/last-name. Serves the
    /// <c>GET /api/users?query=...</c> and <c>?filterProperty=&amp;filter=</c> search contract.
    /// </summary>
    // MIGRATION: Users.ascx.vb ddlSearchType + txtSearch (GetUsersByUserName / GetUsersByEmail / name
    // search), now performed server-side (AAP §0.7.2) rather than being silently ignored by the list
    // endpoint. The optional portalId preserves the caller's per-portal authorization scoping.
    Task<IEnumerable<UserDto>> SearchAsync(int? portalId, string? query, string? filterProperty, string? filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single bounded page of users together with the total user count, for server-side
    /// pagination of <c>GET /api/users</c>.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetAllAsync"/>.
    Task<PagedResult<UserDto>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single bounded page of users belonging to the specified portal together with the total
    /// count for that portal.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetByPortalAsync"/>.
    Task<PagedResult<UserDto>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single bounded page of users matching the supplied search (field-specific or free-text),
    /// optionally portal-scoped, together with the total match count.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="SearchAsync"/>.
    Task<PagedResult<UserDto>> SearchPagedAsync(int? portalId, string? query, string? filterProperty, string? filter, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user and returns the created projection together with the one-time
    /// server-generated password when <c>RandomPassword = true</c> (otherwise the generated password
    /// is <c>null</c>).
    /// </summary>
    // MIGRATION QA finding K: the return type is a CreateUserResult (not a bare UserDto) so a
    // server-generated random password can be surfaced to the caller exactly once without leaking a
    // credential into the persisted/returned UserDto. Throws ConflictException (409) when the
    // (PortalID, Username) pair already exists (finding J — username uniqueness).
    Task<CreateUserResult> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the user with the given id, or returns <c>null</c> if it does not exist.</summary>
    Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user with the given id; returns <c>true</c> if deleted, <c>false</c> if not found.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Changes a user's password; returns <c>true</c> on success.</summary>
    // MIGRATION: legacy UserController.ChangePassword(user, oldPassword, newPassword) [L103]; the new
    // hash is produced by the IPasswordHasher port (BCrypt) in the implementation.
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
}
