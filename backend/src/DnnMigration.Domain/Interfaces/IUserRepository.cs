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
    // isolation (AAP 0.7.1). Retained for internal all-portal reads (e.g. the create-time duplicate-username scan);
    // list endpoints use the paged overload below to avoid over-fetching.
    Task<IEnumerable<User>> GetByPortalIdAsync(int portalId);

    // MIGRATION: CP1 review (performance #22 — avoid over-fetching, AAP 0.7.7) — paged portal-scoped query returning
    // ONLY the requested page plus the total count, replacing the previous fetch-all-then-page-in-memory in
    // UserService.GetByPortalAsync. Mirrors the legacy GetUsers(portalId, pageIndex, pageSize, ByRef totalRecords)
    // MembershipProvider paging (zero-based page index preserved for parity).
    Task<(IEnumerable<User> Items, int TotalCount)> GetByPortalPagedAsync(int portalId, int pageIndex, int pageSize);

    // MIGRATION: Replaces UserController.GetUser(PortalId, UserId). PORTAL-SCOPED (multi-tenant isolation, AAP 0.7.1):
    // CP1 review (IUserRepository #1) — the lookup MUST carry portalId so the service can enforce that a user is only
    // read within its owning portal. (The earlier userId-only "globally-unique identity column" collapse is rejected
    // by the review because it cannot express the legacy portal-scoped operation.) Returns null when no user with that
    // id exists IN that portal.
    Task<User?> GetByIdAsync(int portalId, int userId);

    // MIGRATION: Replaces UserController.GetUserByUsername(portalId, username). Portal-scoped because usernames are
    // unique only within a portal. Returns null when no match. (Consumed by the Application AuthService for login
    // lookup; credential VERIFICATION still happens in Infrastructure Identity, not here.)
    Task<User?> GetByUsernameAsync(int portalId, string username);

    // MIGRATION: Replaces UserController.AddUser(objUser). Returns the persisted User so the database-generated
    // UserId is available. No password material is accepted here (handled by Infrastructure Identity, AAP 0.7.6).
    Task<User> AddAsync(User user);

    // MIGRATION: Replaces UserController.UpdateUser(objUser).
    Task UpdateAsync(User user);

    // MIGRATION: Replaces UserController.DeleteUser(PortalId, UserId). PORTAL-SCOPED (CP1 review IUserRepository #1):
    // carries portalId so a delete is constrained to the owning portal (the userId-only collapse is rejected — it
    // cannot enforce the legacy portal-scoped delete).
    Task DeleteAsync(int portalId, int userId);

    // MIGRATION (CP-final review - profile workflow parity): the EXISTING DNN profile EAV is read/written through
    // these methods (replacing legacy ProfileController.GetPropertyDefinitionsByPortal / GetUserProfile /
    // UpdateUserProfile). GetProfileDefinitionsAsync returns the portal's non-deleted property DEFINITIONS (ordered
    // by ViewOrder, as the legacy collection was); GetProfileValuesAsync returns the user's stored VALUE rows
    // (TRACKED so an in-place update is staged by the unit of work); AddProfileValueAsync stages a new value row.
    // STAGE-ONLY for writes: the Application UserService is the single commit boundary (IUnitOfWork.SaveChangesAsync).
    Task<IReadOnlyList<ProfilePropertyDefinition>> GetProfileDefinitionsAsync(int portalId);

    Task<IReadOnlyList<UserProfileValue>> GetProfileValuesAsync(int userId);

    Task AddProfileValueAsync(UserProfileValue value);
}
