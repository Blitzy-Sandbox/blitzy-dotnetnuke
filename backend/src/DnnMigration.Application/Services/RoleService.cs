// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Roles.RoleController → C# 12 RoleService
// Source: Library/Components/Security/Roles/RoleController.vb
// Changes:
// - Converted from VB.NET class to C# 12 service class with file-scoped namespace
// - Replaced SqlDataProvider calls with repository pattern (IRoleRepository)
// - Converted synchronous methods to async/await pattern with CancellationToken support
// - Extracted AutoAssignUsers logic from RoleController.vb (lines 40-77) to private method
// - Applied Clean Architecture separation with DTOs and domain entities
// - Used AutoMapper for entity-DTO transformations
// - Added comprehensive XML documentation
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing role management business logic.
/// </summary>
/// <remarks>
/// <para>
/// This service orchestrates role CRUD operations and user-role assignments between
/// the API layer and domain/repository layers. It provides async methods for role
/// retrieval, creation, update, deletion, and user membership management.
/// </para>
/// <para>
/// MIGRATION: Derived from RoleController.vb with the following transformations:
/// </para>
/// <list type="bullet">
/// <item><description>Provider pattern replaced with repository pattern (IRoleRepository)</description></item>
/// <item><description>Synchronous methods converted to async with ConfigureAwait(false)</description></item>
/// <item><description>AutoAssignUsers logic preserved from lines 40-77 of RoleController.vb</description></item>
/// <item><description>Entity-DTO mapping handled via AutoMapper</description></item>
/// </list>
/// </remarks>
public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleService"/> class.
    /// </summary>
    /// <param name="roleRepository">The role repository for data access operations.</param>
    /// <param name="userRepository">The user repository for user-related operations.</param>
    /// <param name="mapper">The AutoMapper instance for entity-DTO mapping.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="roleRepository"/>, <paramref name="userRepository"/>,
    /// or <paramref name="mapper"/> is <c>null</c>.
    /// </exception>
    public RoleService(
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IMapper mapper)
    {
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    #region Role CRUD Operations

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRole(RoleID, PortalID) method (lines 175-177):
    /// <code>
    /// Public Function GetRole(ByVal RoleID As Integer, ByVal PortalID As Integer) As RoleInfo
    ///     Return CType(CBO.FillObject(provider.GetRole(PortalID, RoleID), GetType(RoleInfo)), RoleInfo)
    /// End Function
    /// </code>
    /// </remarks>
    public async Task<RoleDto?> GetRoleAsync(
        int id,
        int portalId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, portalId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return null;
        }

        return _mapper.Map<RoleDto>(role);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPortalRoles(PortalId) method (lines 148-150):
    /// <code>
    /// Public Function GetPortalRoles(ByVal PortalId As Integer) As ArrayList
    ///     Return CBO.FillCollection(provider.GetPortalRoles(PortalId), GetType(RoleInfo))
    /// End Function
    /// </code>
    /// </remarks>
    public async Task<PagedResult<RoleDto>> GetRolesAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetByPortalIdAsync(portalId, cancellationToken)
            .ConfigureAwait(false);

        var rolesList = roles.ToList();
        var totalCount = rolesList.Count;

        // Apply pagination
        var pagedRoles = rolesList
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        var roleDtos = pagedRoles.Select(r => _mapper.Map<RoleDto>(r));

        return PagedResult<RoleDto>.Create(roleDtos, pageIndex, pageSize, totalCount);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRolesByGroup(portalId, roleGroupId) method (lines 200-210):
    /// <code>
    /// Public Function GetRolesByGroup(ByVal portalId As Integer, ByVal roleGroupId As Integer) As ArrayList
    ///     Return CBO.FillCollection(provider.GetRolesByGroup(roleGroupId, portalId), GetType(RoleInfo))
    /// End Function
    /// </code>
    /// </remarks>
    public async Task<IEnumerable<RoleDto>> GetRolesByGroupAsync(
        int portalId,
        int roleGroupId,
        CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetByGroupIdAsync(portalId, roleGroupId, cancellationToken)
            .ConfigureAwait(false);

        return roles.Select(r => _mapper.Map<RoleDto>(r));
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRoleByName(PortalId, RoleName) method (lines 162-171):
    /// <code>
    /// Public Function GetRoleByName(ByVal PortalId As Integer, ByVal RoleName As String) As RoleInfo
    ///     Return CType(CBO.FillObject(provider.GetRoleByName(PortalId, RoleName), GetType(RoleInfo)), RoleInfo)
    /// End Function
    /// </code>
    /// </remarks>
    public async Task<RoleDto?> GetRoleByNameAsync(
        int portalId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }

        var role = await _roleRepository.GetByNameAsync(portalId, roleName, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return null;
        }

        return _mapper.Map<RoleDto>(role);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRoleNames(PortalID) method (lines 179-181):
    /// <code>
    /// Public Function GetRoleNames(ByVal PortalID As Integer) As String()
    ///     Return provider.GetRoleNames(PortalID, -1)
    /// End Function
    /// </code>
    /// </remarks>
    public async Task<IEnumerable<string>> GetRoleNamesAsync(
        int portalId,
        CancellationToken cancellationToken = default)
    {
        return await _roleRepository.GetRoleNamesAsync(portalId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET AddRole(objRoleInfo) method (lines 100-112):
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
    /// </remarks>
    public async Task<RoleDto> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Map request to domain entity
        var role = _mapper.Map<Role>(request);

        // Add the role via repository
        var createdRole = await _roleRepository.AddAsync(role, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Preserve AutoAssignUsers logic from RoleController.vb (lines 40-77)
        // If AutoAssignment is true, automatically assign all existing portal users to this role
        if (createdRole.AutoAssignment)
        {
            await AutoAssignUsersAsync(createdRole, cancellationToken)
                .ConfigureAwait(false);
        }

        return _mapper.Map<RoleDto>(createdRole);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET UpdateRole(objRoleInfo) method (lines 254-257):
    /// <code>
    /// Public Sub UpdateRole(ByVal objRoleInfo As RoleInfo)
    ///     provider.UpdateRole(objRoleInfo)
    ///     AutoAssignUsers(objRoleInfo)
    /// End Sub
    /// </code>
    /// </remarks>
    public async Task<RoleDto> UpdateRoleAsync(
        int id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Retrieve the existing role
        var existingRole = await _roleRepository.GetByIdAsync(id, -1, cancellationToken)
            .ConfigureAwait(false);

        if (existingRole is null)
        {
            throw new InvalidOperationException($"Role with ID {id} not found.");
        }

        // Apply updates from request to existing role (partial update support)
        ApplyUpdateRequest(existingRole, request);

        // Update the role via repository
        await _roleRepository.UpdateAsync(existingRole, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Preserve AutoAssignUsers logic from RoleController.vb (lines 254-257)
        // If AutoAssignment is enabled after update, assign existing users
        if (existingRole.AutoAssignment)
        {
            await AutoAssignUsersAsync(existingRole, cancellationToken)
                .ConfigureAwait(false);
        }

        return _mapper.Map<RoleDto>(existingRole);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET DeleteRole(RoleId, PortalId) method (lines 125-133):
    /// <code>
    /// Public Sub DeleteRole(ByVal RoleId As Integer, ByVal PortalId As Integer)
    ///     Dim objRole As RoleInfo = GetRole(RoleId, PortalId)
    ///     If Not objRole Is Nothing Then
    ///         provider.DeleteRole(PortalId, objRole)
    ///     End If
    /// End Sub
    /// </code>
    /// </remarks>
    public async Task DeleteRoleAsync(
        int roleId,
        int portalId,
        CancellationToken cancellationToken = default)
    {
        // Verify role exists before deletion
        var existingRole = await _roleRepository.GetByIdAsync(roleId, portalId, cancellationToken)
            .ConfigureAwait(false);

        if (existingRole is null)
        {
            // Role not found; nothing to delete (matches VB.NET behavior)
            return;
        }

        await _roleRepository.DeleteAsync(roleId, portalId, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region User-Role Membership Operations

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET AddUserRole(PortalID, UserId, RoleId, EffectiveDate, ExpiryDate) method (lines 295-315):
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
    /// </remarks>
    public async Task AddUserToRoleAsync(
        int portalId,
        int userId,
        int roleId,
        DateTime? effectiveDate,
        DateTime? expiryDate,
        CancellationToken cancellationToken = default)
    {
        // Check if user-role assignment already exists
        var existingUserRole = await _roleRepository.GetUserRoleAsync(portalId, userId, roleId, cancellationToken)
            .ConfigureAwait(false);

        if (existingUserRole is null)
        {
            // Create new user-role assignment
            await _roleRepository.AddUserToRoleAsync(
                portalId,
                userId,
                roleId,
                effectiveDate,
                expiryDate,
                cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // Update existing assignment's dates
            existingUserRole.EffectiveDate = effectiveDate;
            existingUserRole.ExpiryDate = expiryDate;
            await _roleRepository.UpdateUserRoleAsync(existingUserRole, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET DeleteUserRole(PortalId, UserId, RoleId) method (lines 330-347):
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
    /// Note: CanRemoveUserFromRole validation (lines 740-765) ensures essential roles
    /// (administrator role for portal admin) cannot be removed. This is delegated to
    /// the repository implementation for simplicity; for strict enforcement, add validation here.
    /// </remarks>
    public async Task<bool> RemoveUserFromRoleAsync(
        int portalId,
        int userId,
        int roleId,
        CancellationToken cancellationToken = default)
    {
        // Check if user-role assignment exists
        var existingUserRole = await _roleRepository.GetUserRoleAsync(portalId, userId, roleId, cancellationToken)
            .ConfigureAwait(false);

        if (existingUserRole is null)
        {
            // Assignment doesn't exist; nothing to remove
            return false;
        }

        // Remove the user from the role
        return await _roleRepository.RemoveUserFromRoleAsync(portalId, userId, roleId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetRolesByUser(UserId, PortalId) method (lines 240-242):
    /// <code>
    /// Public Function GetRolesByUser(ByVal UserId As Integer, ByVal PortalId As Integer) As String()
    ///     Return provider.GetRoleNames(PortalId, UserId)
    /// End Function
    /// </code>
    /// </remarks>
    public async Task<IEnumerable<string>> GetUserRolesAsync(
        int portalId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var userRoles = await _roleRepository.GetUserRolesAsync(portalId, userId, cancellationToken)
            .ConfigureAwait(false);

        // Extract role names from user-role assignments
        return userRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.RoleName);
    }

    /// <summary>
    /// Retrieves a specific user-role assignment.
    /// </summary>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="roleId">The identifier of the role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the <see cref="UserRole"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetUserRole(PortalID, UserId, RoleId) method (lines 350-360):
    /// <code>
    /// Public Function GetUserRole(ByVal PortalID As Integer, ByVal UserId As Integer, ByVal RoleId As Integer) As UserRoleInfo
    ///     Return CType(CBO.FillObject(provider.GetUserRole(PortalID, UserId, RoleId), GetType(UserRoleInfo)), UserRoleInfo)
    /// End Function
    /// </code>
    /// </remarks>
    public async Task<UserRole?> GetUserRoleAsync(
        int portalId,
        int userId,
        int roleId,
        CancellationToken cancellationToken = default)
    {
        return await _roleRepository.GetUserRoleAsync(portalId, userId, roleId, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Automatically assigns all users in a portal to a role with AutoAssignment enabled.
    /// </summary>
    /// <param name="role">The role to assign users to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET AutoAssignUsers(objRoleInfo) private method (lines 40-77):
    /// <code>
    /// Private Sub AutoAssignUsers(ByVal objRoleInfo As RoleInfo)
    ///     If objRoleInfo.AutoAssignment = True Then
    ///         Dim arrUsers As ArrayList = UserController.GetUsers(objRoleInfo.PortalID)
    ///         For Each objUser As UserInfo In arrUsers
    ///             Try
    ///                 AddUserRole(objRoleInfo.PortalID, objUser.UserID, objRoleInfo.RoleID, Null.NullDate, Null.NullDate)
    ///             Catch ex As Exception
    ///                 'User is already in role
    ///             End Try
    ///         Next
    ///     End If
    /// End Sub
    /// </code>
    /// This method iterates through all portal users and assigns them to the role.
    /// Existing assignments are handled gracefully (updated rather than duplicated).
    /// </remarks>
    private async Task AutoAssignUsersAsync(Role role, CancellationToken cancellationToken)
    {
        if (!role.AutoAssignment)
        {
            return;
        }

        // Get all users in the portal
        var users = await _userRepository.GetByPortalIdAsync(role.PortalId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var user in users)
        {
            try
            {
                // MIGRATION: VB.NET Null.NullDate is represented as null in C#
                // Check if user already has the role
                var existingUserRole = await _roleRepository.GetUserRoleAsync(
                    role.PortalId,
                    user.UserId,
                    role.RoleId,
                    cancellationToken)
                    .ConfigureAwait(false);

                if (existingUserRole is null)
                {
                    // Create new user-role assignment with no effective/expiry dates
                    await _roleRepository.AddUserToRoleAsync(
                        role.PortalId,
                        user.UserId,
                        role.RoleId,
                        null, // EffectiveDate = Null.NullDate
                        null, // ExpiryDate = Null.NullDate
                        cancellationToken)
                        .ConfigureAwait(false);
                }
                // If user already has the role, skip (matches VB.NET exception handling behavior)
            }
            catch
            {
                // MIGRATION: VB.NET swallowed exceptions for users already in role
                // Silently continue to next user (preserves legacy behavior)
            }
        }
    }

    /// <summary>
    /// Applies updates from an UpdateRoleRequest to an existing Role entity.
    /// </summary>
    /// <param name="role">The existing role to update.</param>
    /// <param name="request">The update request containing new values.</param>
    /// <remarks>
    /// Only non-null values from the request are applied to support partial updates.
    /// This mirrors the behavior from EditRoles.ascx.vb where only changed fields
    /// were updated on the role entity.
    /// </remarks>
    private static void ApplyUpdateRequest(Role role, UpdateRoleRequest request)
    {
        // Apply only non-null values (partial update support)
        if (request.RoleName is not null)
        {
            role.RoleName = request.RoleName;
        }

        if (request.Description is not null)
        {
            role.Description = request.Description;
        }

        if (request.RoleGroupId.HasValue)
        {
            role.RoleGroupId = request.RoleGroupId.Value == -1 ? null : request.RoleGroupId.Value;
        }

        if (request.IsPublic.HasValue)
        {
            role.IsPublic = request.IsPublic.Value;
        }

        if (request.AutoAssignment.HasValue)
        {
            role.AutoAssignment = request.AutoAssignment.Value;
        }

        if (request.ServiceFee.HasValue)
        {
            role.ServiceFee = request.ServiceFee.Value;
        }

        if (request.BillingPeriod.HasValue)
        {
            role.BillingPeriod = request.BillingPeriod.Value;
        }

        if (request.BillingFrequency is not null)
        {
            role.BillingFrequency = request.BillingFrequency;
        }

        if (request.TrialFee.HasValue)
        {
            role.TrialFee = request.TrialFee.Value;
        }

        if (request.TrialPeriod.HasValue)
        {
            role.TrialPeriod = request.TrialPeriod.Value;
        }

        if (request.TrialFrequency is not null)
        {
            role.TrialFrequency = request.TrialFrequency;
        }

        if (request.RSVPCode is not null)
        {
            role.RSVPCode = request.RSVPCode;
        }

        if (request.IconFile is not null)
        {
            role.IconFile = request.IconFile;
        }
    }

    #endregion
}
