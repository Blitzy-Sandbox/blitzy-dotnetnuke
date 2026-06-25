using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

// MIGRATION: Abstracts the user data-access surface of the legacy UserController.vb (DNN 4.x persisted users via
// a separate MembershipProvider, NOT the DataProvider). Replaces the ArrayList/IDataReader pipeline with async
// LINQ returning User entities. Concrete EF Core implementation lives in
// DnnMigration.Infrastructure/Repositories/UserRepository.cs. Users are portal-scoped (multi-tenant, AAP 0.7.1).
// MIGRATION (SECURITY, AAP 0.7.6): credential concerns are intentionally ABSENT from this contract. Password
// hashing/verification (BCrypt) and token issuance live in the Infrastructure Identity layer and the Application
// AuthService — this repository handles user profile/account data only.
public interface IUserRepository
{
    // MIGRATION: Replaces UserController.GetUsers(portalId) (returned ArrayList). Portal-scoped for multi-tenant
    // isolation (AAP 0.7.1).
    Task<IEnumerable<User>> GetByPortalIdAsync(int portalId);

    // MIGRATION: Replaces UserController.GetUser(portalId, userId). Collapsed to a userId-only lookup (UserId is a
    // globally-unique identity column). Returns null when not found.
    Task<User?> GetByIdAsync(int userId);

    // MIGRATION: Replaces UserController.GetUserByUsername(portalId, username). Portal-scoped because usernames are
    // unique only within a portal. Returns null when no match. (Consumed by the Application AuthService for login
    // lookup; credential VERIFICATION still happens in Infrastructure Identity, not here.)
    Task<User?> GetByUsernameAsync(int portalId, string username);

    // MIGRATION: Replaces UserController.AddUser(objUser). Returns the persisted User so the database-generated
    // UserId is available. No password material is accepted here (handled by Infrastructure Identity, AAP 0.7.6).
    Task<User> AddAsync(User user);

    // MIGRATION: Replaces UserController.UpdateUser(objUser).
    Task UpdateAsync(User user);

    // MIGRATION: Replaces UserController.DeleteUser(portalId, userId). Collapsed to a userId-only delete.
    Task DeleteAsync(int userId);
}
