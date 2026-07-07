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
}
