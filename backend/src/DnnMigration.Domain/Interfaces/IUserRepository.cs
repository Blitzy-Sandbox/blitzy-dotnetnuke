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

    // MIGRATION (DEV-066): User uses a NON-destructive (soft) delete. The DNN 4.9.0.85 [Users] table has NO
    // IsDeleted column and ADR-002 forbids adding one, so the Infrastructure implementation realizes the soft
    // delete schema-faithfully by removing the user's [UserPortals] association row(s) — de-authorizing the
    // account and excluding it from portal-scoped queries while preserving the durable [Users] / aspnet_* rows.
    // This yields the same observable "excluded from the portal list" outcome as the legacy delete without
    // destroying data. Documented in MIGRATION_NOTES.md.
    /// <summary>Soft-deletes a user by id (removes portal membership; preserves identity). (legacy UserController.DeleteUser)</summary>
    Task DeleteAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ASP.NET Membership state row for a user (approval / lockout), or <c>null</c> when the user or
    /// its membership row is absent. MIGRATION (DEV-067): sources the authorize/lockout state from the physical
    /// [aspnet_Membership] table via the lowered-username bridge ([Users].Username -> [aspnet_Users] ->
    /// [aspnet_Membership]); read projection for the Membership workflow (Website/admin/Users/Membership.ascx.vb).
    /// </summary>
    Task<AspNetMembership?> GetMembershipAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the user's membership approval (and the parity [UserPortals].Authorised flag); returns <c>false</c>
    /// when no membership row exists. MIGRATION (DEV-067): the legacy authorize / unauthorize transitions
    /// (Membership.ascx.vb cmdAuthorize / cmdUnAuthorize).
    /// </summary>
    Task<bool> SetApprovedAsync(int userId, bool approved, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the user's lockout (IsLockedOut, FailedPasswordAttemptCount, LastLockoutDate sentinel); returns
    /// <c>false</c> when no membership row exists. MIGRATION (DEV-067): the legacy unlock transition
    /// (Membership.ascx.vb cmdUnLock), faithful to aspnet_Membership_UnlockUser.
    /// </summary>
    Task<bool> UnlockAsync(int userId, CancellationToken cancellationToken = default);
}
