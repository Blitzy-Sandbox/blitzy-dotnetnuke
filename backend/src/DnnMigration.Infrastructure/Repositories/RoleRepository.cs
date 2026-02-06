// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Roles.RoleController → C# 12 RoleRepository
// Source: Library/Components/Security/Roles/RoleController.vb
//
// Key transformations:
// 1) Replace provider.CreateRole (lines 100-112) with AddAsync + SaveChangesAsync
// 2) Replace provider.DeleteRole (lines 125-133) with Remove + SaveChangesAsync
// 3) Replace provider.GetRoles (lines 146-148) with ToListAsync LINQ
// 4) Replace provider.GetRole by ID (lines 163-165) with FirstOrDefaultAsync
// 5) Replace provider.GetRole by name (lines 179-181) with Where(r => r.RoleName == name)
// 6) Replace provider.GetRoleNames (lines 194-196) with Select projection
// 7) Replace provider.GetRolesByGroup (lines 224-226) with Where(r => r.RoleGroupId == groupId)
// 8) Replace provider.UpdateRole (lines 254-257) with Update + SaveChangesAsync
// 9) Convert GetRolesByUser (lines 240-242) to join query via UserRoles
// 10) Convert AddUserRole (lines 295-315) to UserRole entity Add
// 11) Convert DeleteUserRole (lines 330-347) to UserRole entity Remove
// 12) Convert GetUserRole (lines 362-364) to FirstOrDefaultAsync with composite key
// 13) Convert GetUserRoles (lines 392-394) to Where(ur => ur.UserId == userId)
//
// Original VB.NET patterns replaced:
// - Private Shared provider As RoleProvider = RoleProvider.Instance()
// - provider.CreateRole(objRoleInfo.PortalID, objRoleInfo)
// - provider.DeleteRole(PortalId, objRole)
// - provider.GetRoles(PortalId)
// - provider.GetRole(PortalID, RoleID)
// - provider.GetRole(PortalId, RoleName)
// - provider.GetRoleNames(PortalID)
// - provider.GetRolesByGroup(portalId, roleGroupId)
// - provider.UpdateRole(objRoleInfo)
// - provider.GetUserRole(PortalID, UserId, RoleId)
// - provider.GetUserRoles(PortalId, UserId, includePrivate)
// - provider.AddUserToRole(PortalID, objUser, objUserRole)
// - provider.RemoveUserFromRole(PortalId, objUser, objUserRole)
// - provider.UpdateUserRole(userRole)
//
// All these patterns are now replaced by:
// - DbSet<Role>.Add/Update/Remove for CRUD operations
// - DbSet<UserRole>.Add/Update/Remove for user-role membership
// - LINQ queries (Where, FirstOrDefault, ToListAsync, etc.) for data retrieval
// - EF Core change tracking for transaction management
// - SaveChangesAsync for persisting changes
//
// Note: AutoAssignment functionality (lines 68-83) is preserved as reference
// comments but should be implemented in the service layer, not the repository.
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="Role"/> entity CRUD operations using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// This repository replaces the legacy VB.NET RoleController.vb RoleProvider patterns with
/// modern EF Core 8 LINQ queries using <see cref="DnnDbContext"/>. It provides both Role CRUD
/// operations and UserRole membership management operations.
/// </para>
/// <para>
/// <strong>MIGRATION NOTE:</strong> The original RoleController.vb used a provider abstraction
/// pattern with RoleProvider.Instance() for all database operations. This repository eliminates
/// that abstraction layer and directly uses EF Core for improved performance and maintainability.
/// </para>
/// <para>
/// <strong>AutoAssignment:</strong> The original RoleController.vb (lines 68-83) contained
/// AutoAssignUsers logic that automatically assigned existing users to roles with AutoAssignment=true.
/// This functionality should be implemented in the RoleService (application layer), not in this
/// repository, to maintain separation of concerns.
/// </para>
/// <para>
/// <strong>Performance Considerations:</strong>
/// <list type="bullet">
/// <item><description>Use AsNoTracking() for read-only queries to avoid change tracking overhead</description></item>
/// <item><description>All queries include portalId filtering for multi-tenant isolation</description></item>
/// <item><description>UserRole queries join through navigation properties for efficient queries</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // DI Registration in Program.cs:
/// builder.Services.AddScoped&lt;IRoleRepository, RoleRepository&gt;();
///
/// // Usage in a service:
/// public class RoleService : IRoleService
/// {
///     private readonly IRoleRepository _roleRepository;
///     
///     public async Task&lt;RoleDto?&gt; GetRoleAsync(int roleId, int portalId, CancellationToken ct)
///     {
///         var role = await _roleRepository.GetByIdAsync(roleId, portalId, ct);
///         return role is null ? null : MapToDto(role);
///     }
/// }
/// </code>
/// </example>
public class RoleRepository : IRoleRepository
{
    private readonly DnnDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleRepository"/> class.
    /// </summary>
    /// <param name="context">The EF Core database context for data access.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    /// MIGRATION: This constructor replaces the original static provider pattern:
    /// <code>Private Shared provider As RoleProvider = RoleProvider.Instance()</code>
    /// with dependency injection of the DbContext for better testability and lifecycle management.
    /// </remarks>
    public RoleRepository(DnnDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Role CRUD Operations

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetRole(RoleID, PortalID) method (lines 163-165):
    /// <code>Public Function GetRole(ByVal RoleID As Integer, ByVal PortalID As Integer) As RoleInfo
    ///     Return provider.GetRole(PortalID, RoleID)
    /// End Function</code>
    /// </para>
    /// <para>
    /// Uses AsNoTracking() for optimal read performance since the entity is not modified.
    /// The portalId parameter ensures multi-tenant isolation.
    /// </para>
    /// </remarks>
    public async Task<Role?> GetByIdAsync(int roleId, int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleId == roleId && r.PortalId == portalId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetRoles() method with no parameters (lines 208-210):
    /// <code>Public Function GetRoles() As ArrayList
    ///     Return provider.GetRoles(Null.NullInteger)
    /// End Function</code>
    /// </para>
    /// <para>
    /// WARNING: This method returns all roles across all portals. Use GetByPortalIdAsync
    /// for portal-specific queries in multi-tenant scenarios.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .OrderBy(r => r.RoleName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetPortalRoles(PortalId) method (lines 146-148):
    /// <code>Public Function GetPortalRoles(ByVal PortalId As Integer) As ArrayList
    ///     Return provider.GetRoles(PortalId)
    /// End Function</code>
    /// </para>
    /// <para>
    /// This is the recommended method for retrieving roles in multi-tenant scenarios.
    /// Results are ordered by RoleName for consistent display ordering.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<Role>> GetByPortalIdAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Where(r => r.PortalId == portalId)
            .OrderBy(r => r.RoleName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetRolesByGroup(portalId, roleGroupId) method (lines 224-226):
    /// <code>Public Function GetRolesByGroup(ByVal portalId As Integer, ByVal roleGroupId As Integer) As ArrayList
    ///     Return provider.GetRolesByGroup(portalId, roleGroupId)
    /// End Function</code>
    /// </para>
    /// <para>
    /// If roleGroupId is -1, returns all roles for the portal (same as GetByPortalIdAsync).
    /// This matches the original VB.NET behavior where -1 indicated "all groups".
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<Role>> GetByGroupIdAsync(int portalId, int roleGroupId, CancellationToken cancellationToken = default)
    {
        var query = _context.Roles
            .AsNoTracking()
            .Where(r => r.PortalId == portalId);

        // MIGRATION: roleGroupId of -1 means all roles (original VB.NET behavior)
        if (roleGroupId != -1)
        {
            query = query.Where(r => r.RoleGroupId == roleGroupId);
        }

        return await query
            .OrderBy(r => r.RoleName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetRoleByName(PortalId, RoleName) method (lines 179-181):
    /// <code>Public Function GetRoleByName(ByVal PortalId As Integer, ByVal RoleName As String) As RoleInfo
    ///     Return provider.GetRole(PortalId, RoleName)
    /// End Function</code>
    /// </para>
    /// <para>
    /// Role names are unique within a portal, so this method returns at most one result.
    /// Uses case-sensitive comparison (database collation dependent).
    /// </para>
    /// </remarks>
    public async Task<Role?> GetByNameAsync(int portalId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }

        return await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.PortalId == portalId && r.RoleName == roleName, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetRoleNames(PortalID) method (lines 194-196):
    /// <code>Public Function GetRoleNames(ByVal PortalID As Integer) As String()
    ///     Return provider.GetRoleNames(PortalID)
    /// End Function</code>
    /// </para>
    /// <para>
    /// Returns only role names as strings, useful for authorization checks where
    /// full Role entity details are not needed.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<string>> GetRoleNamesAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Where(r => r.PortalId == portalId)
            .OrderBy(r => r.RoleName)
            .Select(r => r.RoleName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET AddRole(objRoleInfo) method (lines 100-112):
    /// <code>Public Function AddRole(ByVal objRoleInfo As RoleInfo) As Integer
    ///     Dim roleId As Integer = -1
    ///     Dim success As Boolean = provider.CreateRole(objRoleInfo.PortalID, objRoleInfo)
    ///     If success Then
    ///         AutoAssignUsers(objRoleInfo)
    ///         roleId = objRoleInfo.RoleID
    ///     End If
    ///     Return roleId
    /// End Function</code>
    /// </para>
    /// <para>
    /// NOTE: The original AutoAssignUsers(objRoleInfo) call (lines 68-83) that automatically
    /// assigned existing users to roles with AutoAssignment=true should be implemented
    /// in the service layer after calling this repository method, not within the repository.
    /// </para>
    /// </remarks>
    public async Task<Role> AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await _context.Roles.AddAsync(role, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // MIGRATION: After SaveChangesAsync, EF Core populates the RoleId property
        // This matches the original behavior where roleId = objRoleInfo.RoleID after provider.CreateRole
        return role;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET UpdateRole(objRoleInfo) method (lines 254-257):
    /// <code>Public Sub UpdateRole(ByVal objRoleInfo As RoleInfo)
    ///     provider.UpdateRole(objRoleInfo)
    ///     AutoAssignUsers(objRoleInfo)
    /// End Sub</code>
    /// </para>
    /// <para>
    /// NOTE: The original AutoAssignUsers(objRoleInfo) call should be implemented
    /// in the service layer after calling this repository method. AutoAssignment
    /// logic involves creating UserRole records for all portal users when the
    /// AutoAssignment flag is true on the role.
    /// </para>
    /// </remarks>
    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        _context.Roles.Update(role);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET DeleteRole(RoleId, PortalId) method (lines 125-133):
    /// <code>Public Sub DeleteRole(ByVal RoleId As Integer, ByVal PortalId As Integer)
    ///     Dim objRole As RoleInfo = GetRole(RoleId, PortalId)
    ///     If Not objRole Is Nothing Then
    ///         provider.DeleteRole(PortalId, objRole)
    ///     End If
    /// End Sub</code>
    /// </para>
    /// <para>
    /// Note: Cascading deletion of related UserRole records should be handled by
    /// database cascade rules or explicitly deleted before calling this method.
    /// </para>
    /// </remarks>
    public async Task DeleteAsync(int roleId, int portalId, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleId == roleId && r.PortalId == portalId, cancellationToken)
            .ConfigureAwait(false);

        if (role is not null)
        {
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region User-Role Membership Operations

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetUserRole(PortalID, UserId, RoleId) method (lines 362-364):
    /// <code>Public Function GetUserRole(ByVal PortalID As Integer, ByVal UserId As Integer, ByVal RoleId As Integer) As UserRoleInfo
    ///     Return provider.GetUserRole(PortalID, UserId, RoleId)
    /// End Function</code>
    /// </para>
    /// <para>
    /// The portalId parameter is used to validate the role belongs to the correct portal
    /// through the join with the Roles table, ensuring multi-tenant isolation.
    /// </para>
    /// </remarks>
    public async Task<UserRole?> GetUserRoleAsync(int portalId, int userId, int roleId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => 
                ur.UserId == userId && 
                ur.RoleId == roleId && 
                ur.Role != null && 
                ur.Role.PortalId == portalId, 
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetUserRoles(PortalId, UserId) method (lines 392-394):
    /// <code>Public Function GetUserRoles(ByVal PortalId As Integer, ByVal UserId As Integer) As ArrayList
    ///     Return provider.GetUserRoles(PortalId, UserId, True)
    /// End Function</code>
    /// </para>
    /// <para>
    /// Note: The original includePrivate parameter (always true in the simple overload)
    /// filtered out non-public roles. This implementation returns all user roles and
    /// filtering by IsPublic can be done in the service layer if needed.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<UserRole>> GetUserRolesAsync(int portalId, int userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId && ur.Role != null && ur.Role.PortalId == portalId)
            .OrderBy(ur => ur.Role!.RoleName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from the need to get users assigned to a specific role by roleId.
    /// The original VB.NET code used GetUserRolesByRoleName more frequently, but this
    /// method provides a more direct query when the roleId is already known.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<UserRole>> GetUserRolesByRoleIdAsync(int portalId, int roleId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.User)
            .Include(ur => ur.Role)
            .Where(ur => ur.RoleId == roleId && ur.Role != null && ur.Role.PortalId == portalId)
            .OrderBy(ur => ur.User!.Username)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetUserRolesByRoleName(portalId, roleName) method (lines 441-443):
    /// <code>Public Function GetUserRolesByRoleName(ByVal portalId As Integer, ByVal roleName As String) As ArrayList
    ///     Return provider.GetUserRolesByRoleName(portalId, roleName)
    /// End Function</code>
    /// </para>
    /// <para>
    /// This method is useful when you have the role name but not the role ID.
    /// The query joins through the Role navigation property to filter by name.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<UserRole>> GetUserRolesByRoleNameAsync(int portalId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return Enumerable.Empty<UserRole>();
        }

        return await _context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.User)
            .Include(ur => ur.Role)
            .Where(ur => ur.Role != null && ur.Role.PortalId == portalId && ur.Role.RoleName == roleName)
            .OrderBy(ur => ur.User!.Username)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET AddUserRole(PortalID, UserId, RoleId, EffectiveDate, ExpiryDate) method (lines 295-315):
    /// <code>Public Sub AddUserRole(ByVal PortalID As Integer, ByVal UserId As Integer, ByVal RoleId As Integer, ByVal EffectiveDate As Date, ByVal ExpiryDate As Date)
    ///     Dim objUser As UserInfo = UserController.GetUser(PortalID, UserId, False)
    ///     Dim objUserRole As UserRoleInfo = GetUserRole(PortalID, UserId, RoleId)
    ///     
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
    /// End Sub</code>
    /// </para>
    /// <para>
    /// This implementation preserves the original behavior: if the user is already
    /// assigned to the role, it updates the existing assignment's dates rather than
    /// creating a duplicate.
    /// </para>
    /// </remarks>
    public async Task<UserRole> AddUserToRoleAsync(
        int portalId, 
        int userId, 
        int roleId, 
        DateTime? effectiveDate, 
        DateTime? expiryDate, 
        CancellationToken cancellationToken = default)
    {
        // First, verify the role exists and belongs to the correct portal
        var roleExists = await _context.Roles
            .AnyAsync(r => r.RoleId == roleId && r.PortalId == portalId, cancellationToken)
            .ConfigureAwait(false);

        if (!roleExists)
        {
            throw new InvalidOperationException($"Role with ID {roleId} does not exist in portal {portalId}.");
        }

        // Check if user-role assignment already exists
        var existingUserRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken)
            .ConfigureAwait(false);

        if (existingUserRole is not null)
        {
            // MIGRATION: Update existing assignment (original VB.NET behavior from lines 309-313)
            existingUserRole.EffectiveDate = effectiveDate;
            existingUserRole.ExpiryDate = expiryDate;
            _context.UserRoles.Update(existingUserRole);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existingUserRole;
        }

        // Create new UserRole
        var userRole = new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            EffectiveDate = effectiveDate ?? DateTime.UtcNow,
            ExpiryDate = expiryDate,
            IsTrialUsed = false,
            Subscribed = false,
            CreatedDate = DateTime.UtcNow
        };

        await _context.UserRoles.AddAsync(userRole, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return userRole;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET DeleteUserRole(PortalId, UserId, RoleId) method (lines 330-347):
    /// <code>Public Function DeleteUserRole(ByVal PortalId As Integer, ByVal UserId As Integer, ByVal RoleId As Integer) As Boolean
    ///     Dim objUser As UserInfo = UserController.GetUser(PortalId, UserId, False)
    ///     Dim objUserRole As UserRoleInfo = GetUserRole(PortalId, UserId, RoleId)
    ///     
    ///     Dim objPortals As New PortalController
    ///     Dim blnDelete As Boolean = True
    ///     
    ///     Dim objPortal As PortalInfo = objPortals.GetPortal(PortalId)
    ///     If Not (objPortal Is Nothing OrElse objUserRole Is Nothing) Then
    ///         If CanRemoveUserFromRole(objPortal, UserId, RoleId) Then
    ///             provider.RemoveUserFromRole(PortalId, objUser, objUserRole)
    ///         Else
    ///             blnDelete = False
    ///         End If
    ///     End If
    ///     
    ///     Return blnDelete
    /// End Function</code>
    /// </para>
    /// <para>
    /// Note: The original CanRemoveUserFromRole logic (lines 741-769) checked if the user
    /// was the administrator being removed from the administrator role, or if removing
    /// from the registered users role. This validation should be performed in the service
    /// layer before calling this repository method.
    /// </para>
    /// </remarks>
    public async Task<bool> RemoveUserFromRoleAsync(int portalId, int userId, int roleId, CancellationToken cancellationToken = default)
    {
        // Find the user-role assignment with portal validation through the Role
        var userRole = await _context.UserRoles
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => 
                ur.UserId == userId && 
                ur.RoleId == roleId && 
                ur.Role != null && 
                ur.Role.PortalId == portalId, 
                cancellationToken)
            .ConfigureAwait(false);

        if (userRole is null)
        {
            return false;
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET provider.UpdateUserRole(userRole) calls throughout
    /// the UpdateUserRole method (lines 472-557). This method is used for updating properties
    /// such as EffectiveDate, ExpiryDate, IsTrialUsed, and Subscribed on an existing
    /// user-role assignment.
    /// </para>
    /// <para>
    /// The original VB.NET code in UpdateUserRole (lines 489-557) contained complex billing
    /// frequency calculations for subscription renewals. That business logic should be
    /// implemented in the service layer, not the repository.
    /// </para>
    /// </remarks>
    public async Task UpdateUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userRole);

        _context.UserRoles.Update(userRole);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
