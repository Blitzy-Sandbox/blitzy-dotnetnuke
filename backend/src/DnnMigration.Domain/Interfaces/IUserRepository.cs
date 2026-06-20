using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository abstraction for the <see cref="User"/> aggregate.
/// MIGRATION: extracted from the data-access methods of UserController.vb. Implemented by the Infrastructure
/// layer with EF Core; consumed by the Application UserService/AuthService. Authentication/validation logic
/// (UserLogin/ValidateUser/ChangePassword) is NOT part of this data contract — it lives in AuthService.
/// </summary>
public interface IUserRepository
{
    /// <summary>Gets a user by id, or <c>null</c> if not found. (legacy UserController.GetUser)</summary>
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by username within a portal, or <c>null</c>.
    /// (legacy UserController.GetUserByName / GetUserByUsername)
    /// </summary>
    Task<User?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by exact email within a portal, or <c>null</c>.
    /// MIGRATION: legacy UserController.GetUsersByEmail performed a PAGED pattern-match returning many users;
    /// this contract models the common exact-match single lookup (e.g. uniqueness/registration/reset checks).
    /// Pattern-search-with-paging, if ever needed, is served via GetByPortalAsync filtered at the service layer.
    /// </summary>
    Task<User?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single page of users for a portal, together with the total user count.
    /// (legacy UserController.GetUsers(portalId, pageIndex, pageSize, ByRef totalRecords).)
    /// </summary>
    Task<(IEnumerable<User> Items, int TotalCount)> GetByPortalAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new user and returns the persisted entity (id populated). (legacy UserController.CreateUser / AddUser)</summary>
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing user. (legacy UserController.UpdateUser)</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    // MIGRATION (DEV-039): User uses HARD-delete. The DNN 4.9.0.85 [Users] table has NO IsDeleted column
    // (verified against the install schema), so a soft delete is impossible without a schema change, which
    // ADR-002 forbids; the legacy UserController.DeleteUser likewise hard-deleted via the membership provider.
    // The Infrastructure implementation removes the row. Documented in MIGRATION_NOTES.md.
    /// <summary>Hard-deletes a user by id. (legacy UserController.DeleteUser)</summary>
    Task DeleteAsync(int userId, CancellationToken cancellationToken = default);
}
