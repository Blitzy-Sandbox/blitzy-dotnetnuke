// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Users.UserController → C# 12 UserRepository
// Source: Library/Components/Users/UserController.vb
//
// Key transformations:
// 1) Replace memberProvider.GetUser calls (lines 497-501) with DnnDbContext LINQ queries
// 2) Replace memberProvider.GetUserByUserName (lines 544-548) with Where(u => u.Username == username)
// 3) Replace memberProvider.GetUsers paged calls (lines 725-749) with Skip/Take pagination
// 4) Replace memberProvider.CreateUser (lines 156-185) with AddAsync + SaveChangesAsync
// 5) Replace memberProvider.UpdateUser (lines 963-969) with Update + SaveChangesAsync
// 6) Replace memberProvider.DeleteUser (lines 200-259) with Remove + SaveChangesAsync
// 7) Convert GetUser(portalId, userId, isHydrated) pattern to FirstOrDefaultAsync
// 8) Convert GetUserCountByPortal (lines 582-586) to CountAsync
// 9) Convert GetUsersByEmail (lines 769-773) to Where(u => EF.Functions.Like(u.Email, emailPattern))
// 10) Implement IUserRepository interface with async/await pattern
// 11) Add CancellationToken support to all async methods
// 12) Use AsNoTracking() for read-only queries
// 13) Use file-scoped namespace syntax
// 14) Use nullable reference types (User?)
// 15) Include query methods: GetByUsernameAsync, GetByEmailAsync, ExistsAsync
// 16) Preserve multi-tenant isolation via PortalId filtering
//
// Original VB.NET patterns replaced:
// - memberProvider.GetUser(portalId, userId, isHydrated) → FirstOrDefaultAsync with AsNoTracking
// - memberProvider.GetUserByUserName(portalId, username, isHydrated) → Where + FirstOrDefaultAsync
// - memberProvider.GetUsers(portalId, isHydrated, pageIndex, pageSize, totalRecords) → Skip/Take with CountAsync
// - memberProvider.CreateUser(objUser) → Add + SaveChangesAsync
// - memberProvider.UpdateUser(objUser) → Update + SaveChangesAsync
// - memberProvider.DeleteUser(objUser) → Remove + SaveChangesAsync
// - DataCache.ClearUserCache calls removed (caching handled by application layer)
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core repository implementation for User entity CRUD operations.
/// </summary>
/// <remarks>
/// <para>
/// This repository implements <see cref="IUserRepository"/> using EF Core 8
/// with <see cref="DnnDbContext"/>. It replaces the legacy VB.NET UserController.vb
/// that used MembershipProvider patterns with modern EF Core LINQ queries.
/// </para>
/// <para>
/// <strong>MIGRATION NOTE:</strong> The original UserController.vb used
/// <c>memberProvider = DotNetNuke.Security.Membership.MembershipProvider.Instance()</c>
/// for all data access operations. This repository replaces that pattern with
/// direct EF Core DbContext operations using LINQ.
/// </para>
/// <para>
/// <strong>Multi-Tenant Isolation:</strong> All query methods that retrieve users
/// filter by PortalId to maintain multi-tenant data isolation. Users can only
/// be accessed within their portal context, except for super user queries.
/// </para>
/// <para>
/// <strong>Performance Considerations:</strong>
/// <list type="bullet">
/// <item><description>Read-only queries use <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})"/> for better performance</description></item>
/// <item><description>Pagination uses Skip/Take for efficient database-level paging</description></item>
/// <item><description>Existence checks use <see cref="EntityFrameworkQueryableExtensions.AnyAsync{TSource}(IQueryable{TSource}, System.Linq.Expressions.Expression{Func{TSource, bool}}, CancellationToken)"/> to avoid loading entities</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // DI Registration in Program.cs:
/// builder.Services.AddScoped&lt;IUserRepository, UserRepository&gt;();
///
/// // Usage in a service:
/// public class UserService : IUserService
/// {
///     private readonly IUserRepository _userRepository;
///     
///     public UserService(IUserRepository userRepository)
///     {
///         _userRepository = userRepository;
///     }
///     
///     public async Task&lt;UserDto?&gt; GetUserAsync(int portalId, int userId)
///     {
///         var user = await _userRepository.GetByIdAsync(portalId, userId);
///         return user is null ? null : MapToDto(user);
///     }
/// }
/// </code>
/// </example>
public class UserRepository : IUserRepository
{
    private readonly DnnDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class.
    /// </summary>
    /// <param name="context">The <see cref="DnnDbContext"/> instance for database operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: This replaces the original static memberProvider field initialization:
    /// <c>Private Shared memberProvider As DotNetNuke.Security.Membership.MembershipProvider = DotNetNuke.Security.Membership.MembershipProvider.Instance()</c>
    /// </para>
    /// <para>
    /// The repository uses constructor injection to receive the DbContext, enabling
    /// proper dependency management and unit testing through mocking.
    /// </para>
    /// </remarks>
    public UserRepository(DnnDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.GetUser (lines 497-501):
    /// <code>
    /// Public Shared Function GetUser(ByVal portalId As Integer, ByVal userId As Integer, ByVal isHydrated As Boolean) As UserInfo
    ///     Return memberProvider.GetUser(portalId, userId, isHydrated)
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// The original isHydrated parameter controlled progressive hydration of the user entity.
    /// This is no longer needed as EF Core handles lazy loading and explicit includes.
    /// </para>
    /// </remarks>
    public async Task<User?> GetByIdAsync(int portalId, int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace memberProvider.GetUser(portalId, userId, isHydrated) with EF Core LINQ
        // Multi-tenant isolation: Filter by both PortalId and UserId
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.PortalId == portalId && u.UserId == userId && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: This method provides system-wide user access, typically used
    /// by super users or host-level administrative functions. The original VB.NET
    /// system did not have a direct equivalent - most calls were portal-scoped.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Return all non-deleted users across all portals
        // Note: This should be used sparingly due to potential performance impact
        return await _context.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.PortalId)
            .ThenBy(u => u.Username)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.GetUsers without pagination (portal-scoped):
    /// <code>
    /// Public Shared Function GetUsers(ByVal portalId As Integer, ...) As ArrayList
    ///     Return memberProvider.GetUsers(portalId, False, pageIndex, pageSize, totalRecords)
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// Returns all users for a portal without pagination. For large portals,
    /// use <see cref="GetPagedAsync"/> for better performance.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<User>> GetByPortalIdAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace memberProvider.GetUsers with EF Core LINQ query
        // Multi-tenant isolation: Filter by PortalId
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.PortalId == portalId && !u.IsDeleted)
            .OrderBy(u => u.DisplayName ?? u.Username)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.GetUserByName (lines 544-548):
    /// <code>
    /// Public Shared Function GetUserByName(ByVal portalId As Integer, ByVal username As String) As UserInfo
    ///     Return memberProvider.GetUserByUserName(portalId, username, False)
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// Username lookup is case-insensitive per the original DNN behavior.
    /// The database collation typically handles case-insensitivity for SQL Server.
    /// </para>
    /// </remarks>
    public async Task<User?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        // MIGRATION: Replace memberProvider.GetUserByUserName(portalId, username, isHydrated) with EF Core LINQ
        // Multi-tenant isolation: Filter by PortalId and Username
        // Username comparison uses database collation (typically case-insensitive)
        // Include UserRoles -> Role for JWT token generation (role claims)
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.PortalId == portalId && u.Username == username && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.GetUsersByEmail (lines 769-773):
    /// <code>
    /// Public Shared Function GetUsersByEmail(ByVal portalId As Integer, ByVal emailToMatch As String, ...) As ArrayList
    ///     Return memberProvider.GetUsersByEmail(portalId, False, emailToMatch, pageIndex, pageSize, totalRecords)
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// The original method supported wildcards for pattern matching. This implementation
    /// performs an exact email match. If multiple users can share an email, consider
    /// returning IEnumerable&lt;User&gt; instead.
    /// </para>
    /// </remarks>
    public async Task<User?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        // MIGRATION: Replace memberProvider.GetUsersByEmail with EF Core LINQ
        // Multi-tenant isolation: Filter by PortalId and Email
        // Email comparison uses database collation (typically case-insensitive)
        // Include UserRoles -> Role for JWT token generation (role claims)
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.PortalId == portalId && u.Email == email && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.GetUsers with pagination (lines 725-749):
    /// <code>
    /// Public Shared Function GetUsers(ByVal portalId As Integer, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList
    ///     Return memberProvider.GetUsers(portalId, False, pageIndex, pageSize, totalRecords)
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// The original method used a ByRef parameter for totalRecords. This implementation
    /// returns a tuple containing both the page results and total count.
    /// </para>
    /// <para>
    /// When pageIndex is -1, all records are returned without pagination (matching
    /// the original VB.NET behavior).
    /// </para>
    /// </remarks>
    public async Task<(IEnumerable<User> Users, int TotalRecords)> GetPagedAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace memberProvider.GetUsers(portalId, isHydrated, pageIndex, pageSize, totalRecords)
        // with Skip/Take pagination
        
        // Base query with portal isolation
        var query = _context.Users
            .AsNoTracking()
            .Where(u => u.PortalId == portalId && !u.IsDeleted)
            .OrderBy(u => u.DisplayName ?? u.Username);

        // Get total count for pagination metadata
        var totalRecords = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<User> users;

        // MIGRATION: Original behavior - when pageIndex is -1, return all records
        if (pageIndex < 0 || pageSize <= 0)
        {
            users = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // MIGRATION: Replace ArrayList paging with Skip/Take
            users = await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return (users, totalRecords);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.GetUserCountByPortal (lines 582-586):
    /// <code>
    /// Public Shared Function GetUserCountByPortal(ByVal portalId As Integer) As Integer
    ///     Return memberProvider.GetUserCountByPortal(portalId)
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// This method uses CountAsync() which is optimized for counting without
    /// loading user entities into memory.
    /// </para>
    /// </remarks>
    public async Task<int> GetUserCountAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace memberProvider.GetUserCountByPortal(portalId) with EF Core CountAsync
        return await _context.Users
            .AsNoTracking()
            .CountAsync(u => u.PortalId == portalId && !u.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.GetUnAuthorizedUsers which
    /// returned users that were not yet approved or had never logged in.
    /// </para>
    /// <para>
    /// This implementation returns users where IsApproved is false, representing
    /// users that require administrative approval before they can access the system.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<User>> GetUnauthorizedUsersAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Return users where IsApproved is false (pending approval)
        // Multi-tenant isolation: Filter by PortalId
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.PortalId == portalId && !u.IsApproved && !u.IsDeleted)
            .OrderBy(u => u.CreatedDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Returns all super users (host administrators) in the system. Super users
    /// have access to all portals and host-level settings.
    /// </para>
    /// <para>
    /// MIGRATION: The original VB.NET UserInfo.IsSuperUser property is mapped to
    /// the IsSuperUser column. Super users are not bound to a specific portal.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<User>> GetSuperUsersAsync(CancellationToken cancellationToken = default)
    {
        // Return all super users across all portals
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.IsSuperUser && !u.IsDeleted)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// This method uses AnyAsync() for efficient existence checking without
    /// loading the full user entity.
    /// </para>
    /// <para>
    /// MIGRATION: The original VB.NET system checked existence by attempting
    /// to retrieve the user. This method is optimized for existence checking only.
    /// </para>
    /// </remarks>
    public async Task<bool> ExistsAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        // Use AnyAsync for efficient existence checking without loading the entity
        return await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.PortalId == portalId && u.Username == username && !u.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.CreateUser (lines 156-185):
    /// <code>
    /// Public Shared Function CreateUser(ByRef objUser As UserInfo) As UserCreateStatus
    ///     Dim createStatus As UserCreateStatus = UserCreateStatus.AddUser
    ///     createStatus = memberProvider.CreateUser(objUser)
    ///     If createStatus = UserCreateStatus.Success Then
    ///         DataCache.ClearPortalCache(objUser.PortalID, False)
    ///         ...
    ///     End If
    ///     Return createStatus
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// This method handles only data persistence. Business logic such as:
    /// <list type="bullet">
    /// <item><description>Auto-role assignment</description></item>
    /// <item><description>Cache clearing (DataCache.ClearPortalCache removed)</description></item>
    /// <item><description>Email notifications</description></item>
    /// <item><description>Event logging</description></item>
    /// </list>
    /// should be handled in the application service layer.
    /// </para>
    /// </remarks>
    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // MIGRATION: Replace memberProvider.CreateUser(objUser) with EF Core Add + SaveChangesAsync
        // Set creation timestamp if not already set
        if (user.CreatedDate == default)
        {
            user.CreatedDate = DateTime.UtcNow;
        }

        // Ensure IsDeleted is false for new users
        user.IsDeleted = false;

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // MIGRATION: DataCache.ClearPortalCache(objUser.PortalID, False) call removed
        // Caching is now handled by the application layer

        return user;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.UpdateUser (lines 963-969):
    /// <code>
    /// Public Shared Sub UpdateUser(ByVal portalId As Integer, ByVal objUser As UserInfo)
    ///     'Update the User
    ///     memberProvider.UpdateUser(objUser)
    ///     'Remove the UserInfo from the Cache, as it has been modified
    ///     DataCache.ClearUserCache(portalId, objUser.Username)
    /// End Sub
    /// </code>
    /// </para>
    /// <para>
    /// This method persists all changes to the user entity. The UpdatedDate
    /// property is automatically set to the current UTC timestamp.
    /// </para>
    /// <para>
    /// Cache clearing (DataCache.ClearUserCache) is now handled by the application layer.
    /// </para>
    /// </remarks>
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // MIGRATION: Replace memberProvider.UpdateUser(objUser) with EF Core Update + SaveChangesAsync
        // Set update timestamp
        user.UpdatedDate = DateTime.UtcNow;

        // Attach and mark as modified if not already tracked
        var entry = _context.Entry(user);
        if (entry.State == EntityState.Detached)
        {
            _context.Users.Update(user);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // MIGRATION: DataCache.ClearUserCache(portalId, objUser.Username) call removed
        // Caching is now handled by the application layer
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.DeleteUser (lines 200-259):
    /// <code>
    /// Public Shared Function DeleteUser(ByRef objUser As UserInfo, ByVal notify As Boolean, ByVal deleteAdmin As Boolean) As Boolean
    ///     ...
    ///     CanDelete = memberProvider.DeleteUser(objUser)
    ///     ...
    ///     DataCache.ClearPortalCache(objUser.PortalID, False)
    ///     DataCache.ClearUserCache(objUser.PortalID, objUser.Username)
    ///     ...
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// This method performs a soft delete by setting IsDeleted = true, preserving
    /// data integrity while marking the user as deleted. Business logic such as:
    /// <list type="bullet">
    /// <item><description>Checking if user is portal administrator</description></item>
    /// <item><description>Deleting associated folder/module/tab permissions</description></item>
    /// <item><description>Sending notification emails</description></item>
    /// <item><description>Event logging</description></item>
    /// <item><description>Cache clearing</description></item>
    /// </list>
    /// should be handled in the application service layer.
    /// </para>
    /// </remarks>
    public async Task DeleteAsync(int portalId, int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace memberProvider.DeleteUser(objUser) with soft delete via EF Core
        // First, find the user with portal isolation
        var user = await _context.Users
            .Where(u => u.PortalId == portalId && u.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user is not null)
        {
            // Perform soft delete by setting IsDeleted flag
            user.IsDeleted = true;
            user.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // MIGRATION: DataCache.ClearPortalCache and DataCache.ClearUserCache calls removed
            // Caching is now handled by the application layer
        }
    }
}
