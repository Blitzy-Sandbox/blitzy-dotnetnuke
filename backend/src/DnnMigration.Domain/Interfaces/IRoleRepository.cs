// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Roles.RoleController → C# 12 IRoleRepository interface
// Source: Library/Components/Security/Roles/RoleController.vb
// Changes:
// - Extracted interface from RoleController class methods
// - Converted VB.NET methods to C# async interface contract:
//   * GetRole → GetByIdAsync
//   * GetPortalRoles/GetRoles → GetAllAsync, GetByPortalIdAsync
//   * GetRolesByGroup → GetByGroupIdAsync
//   * GetRoleByName → GetByNameAsync
//   * GetRoleNames → GetRoleNamesAsync
//   * AddRole → AddAsync
//   * UpdateRole → UpdateAsync
//   * DeleteRole → DeleteAsync
//   * GetUserRole → GetUserRoleAsync
//   * GetUserRoles → GetUserRolesAsync
//   * GetUserRolesByRoleName → GetUserRolesByRoleNameAsync
//   * AddUserRole → AddUserToRoleAsync
//   * DeleteUserRole → RemoveUserFromRoleAsync
//   * UpdateUserRole → UpdateUserRoleAsync
// - Added async/await pattern with Task return types
// - Added CancellationToken parameter to all async methods with default value
// - Used nullable reference type Role? for GetByIdAsync return
// - Used file-scoped namespace as per C# 12 standards
// - Added comprehensive XML documentation comments
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository interface defining data access contract for <see cref="Role"/> entities.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides a contract for role data access operations, extracted from the
/// legacy VB.NET RoleController.vb. Implementations should handle role CRUD operations
/// and user-role membership management.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Security.Roles.RoleController.
/// All methods follow async/await patterns with CancellationToken support for
/// cooperative cancellation of long-running database queries.
/// </para>
/// <para>
/// The interface is designed to maintain domain layer independence while enabling
/// the Infrastructure layer to implement role data access using Entity Framework Core.
/// </para>
/// </remarks>
public interface IRoleRepository
{
    #region Role CRUD Operations

    /// <summary>
    /// Retrieves a single role by its identifier within a specific portal.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="portalId">The identifier of the portal that owns the role.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the <see cref="Role"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRole(RoleID, PortalID) method.
    /// The portalId parameter ensures portal-level isolation for multi-tenant scenarios.
    /// </remarks>
    Task<Role?> GetByIdAsync(int roleId, int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all roles across all portals.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a collection of all <see cref="Role"/> entities.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRoles() method (no parameters).
    /// Use with caution in production as this returns roles from all portals.
    /// For portal-scoped queries, use <see cref="GetByPortalIdAsync"/> instead.
    /// </remarks>
    Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all roles for a specific portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a collection of <see cref="Role"/> entities for the portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPortalRoles(PortalId) method.
    /// This is the recommended method for retrieving roles in a multi-tenant context.
    /// </remarks>
    Task<IEnumerable<Role>> GetByPortalIdAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves roles for a specific role group within a portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="roleGroupId">
    /// The identifier of the role group. Use -1 to retrieve all roles regardless of group.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a collection of <see cref="Role"/> entities in the group.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRolesByGroup(portalId, roleGroupId) method.
    /// If roleGroupId is -1, all roles for the portal are returned (same as GetByPortalIdAsync).
    /// </remarks>
    Task<IEnumerable<Role>> GetByGroupIdAsync(int portalId, int roleGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a role by its name within a specific portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="roleName">The name of the role to find.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the <see cref="Role"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRoleByName(PortalId, RoleName) method.
    /// Role names are unique within a portal, so this method returns at most one result.
    /// </remarks>
    Task<Role?> GetByNameAsync(int portalId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all role names for a specific portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains an array of role names for the portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRoleNames(PortalID) method.
    /// Returns only the role names as strings, useful for authorization checks.
    /// </remarks>
    Task<IEnumerable<string>> GetRoleNamesAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new role to the repository.
    /// </summary>
    /// <param name="role">The <see cref="Role"/> entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the added <see cref="Role"/> with its generated identifier.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET AddRole(objRoleInfo) method.
    /// The implementation should set the RoleId property on the entity after insertion.
    /// Note: The original VB.NET method also handled AutoAssignment logic which should
    /// be implemented in the service layer, not the repository.
    /// </remarks>
    Task<Role> AddAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing role in the repository.
    /// </summary>
    /// <param name="role">The <see cref="Role"/> entity with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET UpdateRole(objRoleInfo) method.
    /// Note: The original VB.NET method also handled AutoAssignment logic which should
    /// be implemented in the service layer, not the repository.
    /// </remarks>
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a role from the repository.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to delete.</param>
    /// <param name="portalId">The identifier of the portal that owns the role.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET DeleteRole(RoleId, PortalId) method.
    /// The portalId parameter ensures portal-level isolation for multi-tenant scenarios.
    /// Implementations should handle cascading deletion of related UserRole records
    /// or rely on database cascade rules.
    /// </remarks>
    Task DeleteAsync(int roleId, int portalId, CancellationToken cancellationToken = default);

    #endregion

    #region User-Role Membership Operations

    /// <summary>
    /// Retrieves a specific user-role assignment.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="roleId">The identifier of the role.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the <see cref="UserRole"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetUserRole(PortalID, UserId, RoleId) method.
    /// Used to check if a user has a specific role or to retrieve assignment details
    /// such as EffectiveDate and ExpiryDate.
    /// </remarks>
    Task<UserRole?> GetUserRoleAsync(int portalId, int userId, int roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all user-role assignments for a specific user within a portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a collection of <see cref="UserRole"/> entities for the user.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetUserRoles(PortalId, UserId) method.
    /// Returns all role assignments for a user, including assignment details.
    /// </remarks>
    Task<IEnumerable<UserRole>> GetUserRolesAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all user-role assignments for a specific role within a portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="roleId">The identifier of the role.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a collection of <see cref="UserRole"/> entities for the role.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from the need to get users in a specific role.
    /// Use this to find all users assigned to a particular role.
    /// </remarks>
    Task<IEnumerable<UserRole>> GetUserRolesByRoleIdAsync(int portalId, int roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all user-role assignments for a specific role name within a portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a collection of <see cref="UserRole"/> entities for the role.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetUserRolesByRoleName(portalId, roleName) method.
    /// Convenient method when you have the role name but not the role ID.
    /// </remarks>
    Task<IEnumerable<UserRole>> GetUserRolesByRoleNameAsync(int portalId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user to a role with optional effective and expiry dates.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="roleId">The identifier of the role.</param>
    /// <param name="effectiveDate">
    /// The date when the role assignment becomes active. Use <c>null</c> for immediate activation.
    /// </param>
    /// <param name="expiryDate">
    /// The date when the role assignment expires. Use <c>null</c> for no expiration.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the created <see cref="UserRole"/> assignment.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET AddUserRole(PortalID, UserId, RoleId, EffectiveDate, ExpiryDate) method.
    /// Creates a new user-role assignment with the specified validity period.
    /// If the user is already assigned to the role, implementations should update
    /// the existing assignment's dates rather than creating a duplicate.
    /// </remarks>
    Task<UserRole> AddUserToRoleAsync(int portalId, int userId, int roleId, DateTime? effectiveDate, DateTime? expiryDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user from a role.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="roleId">The identifier of the role.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result is <c>true</c> if the user was removed; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET DeleteUserRole(PortalId, UserId, RoleId) method.
    /// Deletes the user-role assignment if it exists. Returns false if the assignment
    /// was not found or could not be removed (e.g., if it's a required system role).
    /// </remarks>
    Task<bool> RemoveUserFromRoleAsync(int portalId, int userId, int roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user-role assignment.
    /// </summary>
    /// <param name="userRole">The <see cref="UserRole"/> entity with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET UpdateUserRole behavior in AddUserRole method.
    /// Updates properties such as EffectiveDate, ExpiryDate, IsTrialUsed, and Subscribed
    /// on an existing user-role assignment.
    /// </remarks>
    Task UpdateUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default);

    #endregion
}
