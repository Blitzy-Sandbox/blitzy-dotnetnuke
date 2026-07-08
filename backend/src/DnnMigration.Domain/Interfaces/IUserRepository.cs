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
}
