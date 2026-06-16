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
    /// INSERTS a new user-role assignment and returns the persisted join entity (UserRoleID populated).
    /// This is the create-only operation; to update an existing assignment's effective/expiry window use
    /// <see cref="UpdateUserRoleAsync"/>. (legacy RoleController.AddUserRole "If objUserRole Is Nothing" branch →
    /// MembershipProvider.AddUserToRole [RoleController.vb:L300-307])
    /// </summary>
    Task<UserRole> AddUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// UPDATES an existing user-role assignment (its effective/expiry window) and returns the persisted join entity.
    /// Distinct from <see cref="AddUserRoleAsync"/> so callers express create vs update explicitly rather than
    /// overloading "add" for both. (legacy RoleController.AddUserRole "Else" branch →
    /// MembershipProvider.UpdateUserRole [RoleController.vb:L308-313])
    /// </summary>
    Task<UserRole> UpdateUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default);

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
