using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository abstraction for the <see cref="Role"/> aggregate, including user↔role membership and a
/// role-group read. MIGRATION: extracted from the data-access methods of RoleController.vb. Implemented by the
/// Infrastructure layer with EF Core; consumed by the Application RoleService. Membership operations are
/// consolidated here (rather than on IUserRepository) to avoid duplication.
/// </summary>
public interface IRoleRepository
{
    // --- Role CRUD ---

    /// <summary>Gets a role by id, or <c>null</c> if not found. (legacy RoleController.GetRole)</summary>
    Task<Role?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>Gets all roles for a portal. (legacy RoleController.GetPortalRoles)</summary>
    Task<IEnumerable<Role>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new role and returns the persisted entity (id populated). (legacy RoleController.AddRole)</summary>
    Task<Role> AddAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing role. (legacy RoleController.UpdateRole)</summary>
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);

    // MIGRATION: Role uses HARD-delete with cascade (AAP §0.3.3); the cascade (and any user-role cleanup) is
    // performed transactionally in the Infrastructure implementation. Contract only declares the operation.
    /// <summary>Deletes a role by id. (legacy RoleController.DeleteRole)</summary>
    Task DeleteAsync(int roleId, CancellationToken cancellationToken = default);

    // --- User-role membership ---

    /// <summary>
    /// INSERTS a new user→role assignment and returns the persisted join entity (id populated).
    /// MIGRATION (M3/DEV-034): this is an <b>insert-only</b> operation (legacy <c>RoleController.AddUserRole</c> →
    /// <c>provider.AddUserRoleToPortal</c>); it does NOT upsert. For an assignment that already exists, callers
    /// MUST use <see cref="UpdateUserRoleAsync"/> instead, mirroring the legacy add-vs-update split.
    /// </summary>
    Task<UserRole> AddUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// UPDATES an existing user→role assignment (effective/expiry dates, trial-used flag) by its
    /// <c>UserRoleID</c>. MIGRATION (M3/DEV-034): mirrors the legacy <c>RoleController.UpdateUserRole</c> →
    /// <c>provider.UpdateUserRole</c> path that the existing-assignment branch of <c>UpdateUserRole</c> took,
    /// distinct from the insert-only <see cref="AddUserRoleAsync"/>. The EF Core implementation is delivered in a
    /// later checkpoint (no repository implementations exist yet); declaring the contract here lets the
    /// Application <c>RoleService</c> preserve the legacy update semantics rather than mis-routing an existing
    /// assignment through the insert path.
    /// </summary>
    Task UpdateUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all role assignments (join rows, carrying effective/expiry dates and the Role navigation) for a user.
    /// (legacy RoleController.GetUserRoles)
    /// </summary>
    Task<IEnumerable<UserRole>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Gets all users assigned to a role. (legacy RoleController.GetUsersInRole / GetUserRolesByRoleName)</summary>
    Task<IEnumerable<User>> GetUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a role. (legacy RoleController.DeleteUserRole)</summary>
    Task RemoveUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default);

    // --- Role groups (read) ---

    /// <summary>Gets all role groups for a portal. (legacy RoleController.GetRoleGroups)</summary>
    Task<IEnumerable<RoleGroup>> GetRoleGroupsAsync(int portalId, CancellationToken cancellationToken = default);
}
