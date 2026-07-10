using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository contract for the <see cref="User"/> aggregate. Extends the generic
/// <see cref="IRepository{T}"/> CRUD surface with user-specific lookups.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>Retrieves a user within a portal by username, or <c>null</c> if not found.</summary>
    // MIGRATION: legacy UserController.GetUserByName(portalId, username[, isHydrated])
    // [UserController.vb L544/L564], which delegated to MembershipProvider.GetUserByUserName; converted
    // to an async nullable single-entity lookup (implemented over aspnet_Users/aspnet_Membership via
    // EF Core downstream).
    Task<User?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all users belonging to the specified portal.</summary>
    // MIGRATION: legacy UserController.GetUsers(portalId) [UserController.vb L685] returned an ArrayList
    // of UserInfo; converted to an async materialized collection.
    Task<IEnumerable<User>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches users, optionally scoped to a single portal. When <paramref name="filterProperty"/> is
    /// <c>Username</c> or <c>Email</c>, matching is restricted to that single field against
    /// <paramref name="filter"/>; otherwise the free-text <paramref name="query"/> is matched
    /// (case-insensitive substring) across username, email, display name, first name, and last name.
    /// </summary>
    // MIGRATION: the legacy Users.ascx.vb grid's ddlSearchType + txtSearch drove
    // UserController.GetUsersByUserName / GetUsersByEmail (field-specific) and a general name search.
    // Re-expressed here as an async, materialized substring filter (LINQ .ToLower().Contains downstream)
    // so the AAP §0.7.2 "Search/Filter -> GET /api/users?query=... | ?filterProperty=&filter=" contract
    // is served server-side rather than ignored. The optional portalId preserves the per-portal
    // authorization scoping applied by UsersController. The field-specific values ("Username"/"Email")
    // exactly mirror the SPA's ddlSearchType option values.
    Task<IEnumerable<User>> SearchAsync(int? portalId, string? query, string? filterProperty, string? filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of users (ordered by <c>UserID</c>), fully hydrated (PortalID,
    /// membership, profile), together with the total user count, for server-side pagination of
    /// <c>GET /api/users</c>.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="IRepository{T}.GetAllAsync"/>.
    // The full-list GetAllAsync (which drives internal callers and the ~40 mocked unit tests) is left
    // untouched; this fetches only the Skip/Take window (ordered by the UserID primary key) plus a COUNT and
    // still runs the same batched three-query hydration so a paged row carries the identical real membership
    // dates + profile a single GET returns (preserving QA finding F1 fidelity on the paged path).
    Task<PagedResult<User>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of users belonging to the specified portal (ordered by
    /// <c>UserID</c>), fully hydrated, together with the total count for that portal.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetByPortalAsync"/>.
    Task<PagedResult<User>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of users matching the supplied search (field-specific
    /// <paramref name="filterProperty"/>/<paramref name="filter"/> or free-text <paramref name="query"/>),
    /// optionally scoped to a portal, ordered by <c>UserID</c> and fully hydrated, together with the total
    /// match count.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="SearchAsync"/>.
    Task<PagedResult<User>> SearchPagedAsync(int? portalId, string? query, string? filterProperty, string? filter, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every user association to the specified portal and fully cascade-deletes only the users left
    /// orphaned by that removal (i.e. those with no remaining association to any other portal), together
    /// with their credential (<c>aspnet_Membership</c>) and profile (<c>aspnet_Profile</c>) rows. A user
    /// still associated with another portal keeps its identity/credential/profile rows and only loses its
    /// association to this portal.
    /// </summary>
    // MIGRATION (QA finding - R10 Issue 2): restores the portal-scoped user cleanup the legacy
    // PortalController.DeletePortalInfo performed via UserController.DeleteUsers [PortalController.vb L1199]
    // before deleting the portal row [L1202]. In the modern schema there is NO [Users].[PortalID] column and
    // NO DB-level FK cascade from [Portals] to [Users]/[UserPortals], so deleting a portal row alone strands
    // the administrator PortalService.CreateAsync provisioned (its [Users] + [aspnet_Membership] +
    // [aspnet_Profile] rows) and its [UserPortals] junction. This method re-expresses that cleanup as EF
    // Core data access, orphan-aware so a user shared across portals is preserved.
    Task DeleteByPortalAsync(int portalId, CancellationToken cancellationToken = default);
}
