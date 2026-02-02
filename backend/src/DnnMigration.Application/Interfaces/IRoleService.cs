// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) DnnMigration Project. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Service interface derived from RoleController.vb (Library/Components/Security/Roles/RoleController.vb)
// MIGRATION: Operations mapped from legacy controller methods: AddRole, GetRole, GetPortalRoles, GetRoleByName,
//            GetRolesByGroup, DeleteRole, UpdateRole, AddUserRole, DeleteUserRole, GetRolesByUser
// MIGRATION: Section 0.3.3 - Service Layer Pattern with async Task-based methods
// MIGRATION: Section 0.7.2 - Async/Await patterns with CancellationToken support
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service interface for role management operations.
/// Defines async methods for role CRUD operations and user-role assignments.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This interface extracts the role management operations from the legacy
/// DotNetNuke.Security.Roles.RoleController class (RoleController.vb).
/// </para>
/// <para>
/// The interface follows modern .NET patterns:
/// </para>
/// <list type="bullet">
/// <item><description>All methods are async and return <see cref="Task{TResult}"/> or <see cref="Task"/></description></item>
/// <item><description>All methods include <see cref="CancellationToken"/> parameter for cooperative cancellation</description></item>
/// <item><description>Methods return DTOs rather than domain entities for API responses</description></item>
/// <item><description>Enables dependency injection and unit testing of role business logic</description></item>
/// </list>
/// <para>
/// Original VB.NET methods mapped to async equivalents:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Legacy Method</term>
/// <description>New Async Method</description>
/// </listheader>
/// <item><term>GetRole(RoleID, PortalID)</term><description><see cref="GetRoleAsync"/></description></item>
/// <item><term>GetPortalRoles(PortalId)</term><description><see cref="GetRolesAsync"/></description></item>
/// <item><term>GetRolesByGroup(portalId, roleGroupId)</term><description><see cref="GetRolesByGroupAsync"/></description></item>
/// <item><term>GetRoleByName(PortalId, RoleName)</term><description><see cref="GetRoleByNameAsync"/></description></item>
/// <item><term>AddRole(objRoleInfo)</term><description><see cref="CreateRoleAsync"/></description></item>
/// <item><term>UpdateRole(objRoleInfo)</term><description><see cref="UpdateRoleAsync"/></description></item>
/// <item><term>DeleteRole(RoleId, PortalId)</term><description><see cref="DeleteRoleAsync"/></description></item>
/// <item><term>AddUserRole(PortalID, UserId, RoleId, EffectiveDate, ExpiryDate)</term><description><see cref="AddUserToRoleAsync"/></description></item>
/// <item><term>DeleteUserRole(PortalId, UserId, RoleId)</term><description><see cref="RemoveUserFromRoleAsync"/></description></item>
/// <item><term>GetRolesByUser(UserId, PortalId)</term><description><see cref="GetUserRolesAsync"/></description></item>
/// </list>
/// </remarks>
public interface IRoleService
{
    /// <summary>
    /// Retrieves a single role by its unique identifier and portal context.
    /// </summary>
    /// <param name="id">The unique identifier of the role (RoleID).</param>
    /// <param name="portalId">The identifier of the portal context (PortalID).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the <see cref="RoleDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from RoleController.vb GetRole method (lines 163-165):
    /// <code>
    /// Public Function GetRole(ByVal RoleID As Integer, ByVal PortalID As Integer) As RoleInfo
    ///     Return provider.GetRole(PortalID, RoleID)
    /// End Function
    /// </code>
    /// </remarks>
    Task<RoleDto?> GetRoleAsync(int id, int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of roles for a specific portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal to retrieve roles for.</param>
    /// <param name="pageIndex">The zero-based index of the page to retrieve.</param>
    /// <param name="pageSize">The maximum number of roles per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a <see cref="PagedResult{T}"/> of <see cref="RoleDto"/> objects.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from RoleController.vb GetPortalRoles method (lines 146-148):
    /// <code>
    /// Public Function GetPortalRoles(ByVal PortalId As Integer) As ArrayList
    ///     Return provider.GetRoles(PortalId)
    /// End Function
    /// </code>
    /// The legacy method returned an ArrayList without pagination. The new implementation
    /// adds proper pagination support via <see cref="PagedResult{T}"/>.
    /// </remarks>
    Task<PagedResult<RoleDto>> GetRolesAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all roles belonging to a specific role group within a portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="roleGroupId">
    /// The identifier of the role group. Pass -1 to retrieve roles not assigned to any group
    /// (global roles).
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// an enumerable collection of <see cref="RoleDto"/> objects belonging to the specified group.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from RoleController.vb GetRolesByGroup method (lines 224-226):
    /// <code>
    /// Public Function GetRolesByGroup(ByVal portalId As Integer, ByVal roleGroupId As Integer) As ArrayList
    ///     Return provider.GetRolesByGroup(portalId, roleGroupId)
    /// End Function
    /// </code>
    /// The legacy method uses -1 for roleGroupId to indicate global roles (Null.NullInteger pattern).
    /// </remarks>
    Task<IEnumerable<RoleDto>> GetRolesByGroupAsync(int portalId, int roleGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a role by its name within a specific portal context.
    /// </summary>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="roleName">The name of the role to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the <see cref="RoleDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from RoleController.vb GetRoleByName method (lines 179-181):
    /// <code>
    /// Public Function GetRoleByName(ByVal PortalId As Integer, ByVal RoleName As String) As RoleInfo
    ///     Return provider.GetRole(PortalId, RoleName)
    /// End Function
    /// </code>
    /// Role names are unique within a portal context.
    /// </remarks>
    Task<RoleDto?> GetRoleByNameAsync(int portalId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new security role based on the provided request data.
    /// </summary>
    /// <param name="request">
    /// The <see cref="CreateRoleRequest"/> containing the role creation data including
    /// RoleName, Description, RoleGroupId, IsPublic, AutoAssignment, and billing/trial settings.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the newly created <see cref="RoleDto"/> with the assigned RoleId.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from RoleController.vb AddRole method (lines 100-112):
    /// </para>
    /// <code>
    /// Public Function AddRole(ByVal objRoleInfo As RoleInfo) As Integer
    ///     Dim roleId As Integer = -1
    ///     Dim success As Boolean = provider.CreateRole(objRoleInfo.PortalID, objRoleInfo)
    ///     If success Then
    ///         AutoAssignUsers(objRoleInfo)
    ///         roleId = objRoleInfo.RoleID
    ///     End If
    ///     Return roleId
    /// End Function
    /// </code>
    /// <para>
    /// If the role has <c>AutoAssignment = true</c>, existing users in the portal will be
    /// automatically assigned to the role (preserving legacy AutoAssignUsers behavior).
    /// </para>
    /// </remarks>
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing role with the provided request data.
    /// </summary>
    /// <param name="id">The unique identifier of the role to update.</param>
    /// <param name="request">
    /// The <see cref="UpdateRoleRequest"/> containing the updated role data.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the updated <see cref="RoleDto"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from RoleController.vb UpdateRole method (lines 254-257):
    /// </para>
    /// <code>
    /// Public Sub UpdateRole(ByVal objRoleInfo As RoleInfo)
    ///     provider.UpdateRole(objRoleInfo)
    ///     AutoAssignUsers(objRoleInfo)
    /// End Sub
    /// </code>
    /// <para>
    /// If the role has <c>AutoAssignment</c> enabled after the update, existing users
    /// will be automatically assigned to the role (preserving legacy AutoAssignUsers behavior).
    /// </para>
    /// </remarks>
    Task<RoleDto> UpdateRoleAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a role from the system.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to delete.</param>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from RoleController.vb DeleteRole method (lines 125-133):
    /// </para>
    /// <code>
    /// Public Sub DeleteRole(ByVal RoleId As Integer, ByVal PortalId As Integer)
    ///     Dim objRole As RoleInfo = GetRole(RoleId, PortalId)
    ///     If Not objRole Is Nothing Then
    ///         provider.DeleteRole(PortalId, objRole)
    ///     End If
    /// End Sub
    /// </code>
    /// <para>
    /// The operation verifies the role exists before attempting deletion.
    /// User-role associations should be removed before or during role deletion.
    /// </para>
    /// </remarks>
    Task DeleteRoleAsync(int roleId, int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user to a role with optional effective and expiry dates.
    /// </summary>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="userId">The identifier of the user to add to the role.</param>
    /// <param name="roleId">The identifier of the role.</param>
    /// <param name="effectiveDate">
    /// The date when the role membership becomes active. Pass <c>null</c> for immediate activation.
    /// </param>
    /// <param name="expiryDate">
    /// The date when the role membership expires. Pass <c>null</c> for no expiration.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from RoleController.vb AddUserRole method (lines 295-315):
    /// </para>
    /// <code>
    /// Public Sub AddUserRole(ByVal PortalID As Integer, ByVal UserId As Integer, ByVal RoleId As Integer, ByVal EffectiveDate As Date, ByVal ExpiryDate As Date)
    ///     Dim objUser As UserInfo = UserController.GetUser(PortalID, UserId, False)
    ///     Dim objUserRole As UserRoleInfo = GetUserRole(PortalID, UserId, RoleId)
    ///     If objUserRole Is Nothing Then
    ///         'Create new UserRole
    ///         objUserRole = New UserRoleInfo
    ///         objUserRole.UserID = UserId
    ///         objUserRole.RoleID = RoleId
    ///         objUserRole.PortalID = PortalID
    ///         objUserRole.EffectiveDate = EffectiveDate
    ///         objUserRole.ExpiryDate = ExpiryDate
    ///         provider.AddUserToRole(PortalID, objUser, objUserRole)
    ///     Else
    ///         objUserRole.EffectiveDate = EffectiveDate
    ///         objUserRole.ExpiryDate = ExpiryDate
    ///         provider.UpdateUserRole(objUserRole)
    ///     End If
    /// End Sub
    /// </code>
    /// <para>
    /// If the user is already in the role, the effective and expiry dates are updated.
    /// The legacy VB.NET Null.NullDate is represented as <c>null</c> in C#.
    /// </para>
    /// </remarks>
    Task AddUserToRoleAsync(int portalId, int userId, int roleId, DateTime? effectiveDate, DateTime? expiryDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user from a role.
    /// </summary>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="userId">The identifier of the user to remove from the role.</param>
    /// <param name="roleId">The identifier of the role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is <c>true</c>
    /// if the user was successfully removed from the role; <c>false</c> if the removal
    /// was not allowed (e.g., cannot remove administrator from admin role).
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from RoleController.vb DeleteUserRole method (lines 330-347):
    /// </para>
    /// <code>
    /// Public Function DeleteUserRole(ByVal PortalId As Integer, ByVal UserId As Integer, ByVal RoleId As Integer) As Boolean
    ///     Dim objUser As UserInfo = UserController.GetUser(PortalId, UserId, False)
    ///     Dim objUserRole As UserRoleInfo = GetUserRole(PortalId, UserId, RoleId)
    ///     Dim objPortals As New PortalController
    ///     Dim blnDelete As Boolean = True
    ///     Dim objPortal As PortalInfo = objPortals.GetPortal(PortalId)
    ///     If Not (objPortal Is Nothing OrElse objUserRole Is Nothing) Then
    ///         If CanRemoveUserFromRole(objPortal, UserId, RoleId) Then
    ///             provider.RemoveUserFromRole(PortalId, objUser, objUserRole)
    ///         Else
    ///             blnDelete = False
    ///         End If
    ///     End If
    ///     Return blnDelete
    /// End Function
    /// </code>
    /// <para>
    /// The method includes validation via CanRemoveUserFromRole to prevent removal of
    /// essential role assignments (e.g., portal administrator from administrator role).
    /// </para>
    /// </remarks>
    Task<bool> RemoveUserFromRoleAsync(int portalId, int userId, int roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the names of all roles assigned to a specific user within a portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// an enumerable collection of role names assigned to the user.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from RoleController.vb GetRolesByUser method (lines 240-242):
    /// </para>
    /// <code>
    /// Public Function GetRolesByUser(ByVal UserId As Integer, ByVal PortalId As Integer) As String()
    ///     Return provider.GetRoleNames(PortalId, UserId)
    /// End Function
    /// </code>
    /// <para>
    /// The legacy method returned a String array; this method returns <see cref="IEnumerable{T}"/>
    /// of string for better LINQ compatibility and flexibility.
    /// </para>
    /// </remarks>
    Task<IEnumerable<string>> GetUserRolesAsync(int portalId, int userId, CancellationToken cancellationToken = default);
}
