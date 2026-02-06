// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Permissions.PermissionController → C# 12 PermissionRepository
// Source: Library/Components/Security/Permissions/PermissionController.vb
//
// Key transformations:
// 1) Replace DataProvider.Instance().GetPermission with FirstOrDefaultAsync
// 2) Replace DataProvider.Instance().GetPermissionsByModuleDefID with Where/ToListAsync
// 3) Replace DataProvider.Instance().GetPermissionsByModuleID with Where/ToListAsync
// 4) Replace DataProvider.Instance().GetPermissionsByFolderPath with Where/ToListAsync
// 5) Replace DataProvider.Instance().GetPermissionByCodeAndKey with Where/ToListAsync
// 6) Replace DataProvider.Instance().GetPermissionsByTabID with Where/ToListAsync
// 7) Replace DataProvider.Instance().AddPermission with Add + SaveChangesAsync
// 8) Replace DataProvider.Instance().UpdatePermission with Update + SaveChangesAsync
// 9) Replace DataProvider.Instance().DeletePermission with Remove + SaveChangesAsync
//
// Original VB.NET patterns replaced:
// - DataProvider.Instance().GetPermission(permissionID)
// - DataProvider.Instance().GetPermissionsByModuleDefID(ModuleDefID)
// - DataProvider.Instance().GetPermissionsByModuleID(ModuleID)
// - DataProvider.Instance().GetPermissionsByFolderPath(PortalID, Folder)
// - DataProvider.Instance().GetPermissionByCodeAndKey(PermissionCode, PermissionKey)
// - DataProvider.Instance().GetPermissionsByTabID(TabID)
// - DataProvider.Instance().AddPermission(PermissionCode, ModuleDefID, PermissionKey, PermissionName)
// - DataProvider.Instance().UpdatePermission(PermissionID, PermissionCode, ModuleDefID, PermissionKey, PermissionName)
// - DataProvider.Instance().DeletePermission(permissionID)
//
// All these patterns are now replaced by:
// - DbSet<Permission>.Add/Update/Remove for CRUD operations
// - LINQ queries (Where, FirstOrDefaultAsync, ToListAsync) for data retrieval
// - EF Core change tracking for transaction management
// - SaveChangesAsync for persisting changes
// - AsNoTracking() for read-only queries
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="Permission"/> entity CRUD operations using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// This repository replaces the legacy VB.NET PermissionController.vb DataProvider patterns with
/// modern EF Core 8 LINQ queries using <see cref="DnnDbContext"/>. It provides Permission CRUD
/// operations and permission retrieval by various criteria (module definition, module ID, tab, folder, code/key).
/// </para>
/// <para>
/// <strong>MIGRATION NOTE:</strong> The original PermissionController.vb used DataProvider.Instance()
/// for all database operations. This repository eliminates that abstraction layer and directly uses
/// EF Core for improved performance and maintainability.
/// </para>
/// <para>
/// <strong>Performance Considerations:</strong>
/// <list type="bullet">
/// <item><description>Use AsNoTracking() for read-only queries to avoid change tracking overhead</description></item>
/// <item><description>Queries use indexed columns (PermissionId, ModuleDefId, PermissionCode, PermissionKey) for efficient lookup</description></item>
/// <item><description>ConfigureAwait(false) is used per library code patterns</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // DI Registration in Program.cs:
/// builder.Services.AddScoped&lt;IPermissionRepository, PermissionRepository&gt;();
///
/// // Usage in a service:
/// public class PermissionService : IPermissionService
/// {
///     private readonly IPermissionRepository _permissionRepository;
///     
///     public async Task&lt;PermissionDto?&gt; GetPermissionAsync(int permissionId, CancellationToken ct)
///     {
///         var permission = await _permissionRepository.GetByIdAsync(permissionId, ct);
///         return permission is null ? null : MapToDto(permission);
///     }
/// }
/// </code>
/// </example>
public class PermissionRepository : IPermissionRepository
{
    private readonly DnnDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRepository"/> class.
    /// </summary>
    /// <param name="context">The database context for EF Core operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public PermissionRepository(DnnDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermission(permissionID As Integer) As PermissionInfo.
    /// Original: CType(CBO.FillObject(DataProvider.Instance().GetPermission(permissionID), GetType(PermissionInfo)), PermissionInfo)
    /// Now uses EF Core FirstOrDefaultAsync with AsNoTracking for optimal read performance.
    /// </remarks>
    public async Task<Permission?> GetByIdAsync(int permissionId, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PermissionId == permissionId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionsByModuleDefID(ModuleDefID As Integer) As ArrayList.
    /// Original: CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleDefID(ModuleDefID), GetType(PermissionInfo))
    /// Now uses EF Core Where/ToListAsync with AsNoTracking for optimal read performance.
    /// </remarks>
    public async Task<IEnumerable<Permission>> GetByModuleDefIdAsync(int moduleDefId, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => p.ModuleDefId == moduleDefId)
            .OrderBy(p => p.PermissionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionsByModuleID(ModuleID As Integer) As ArrayList.
    /// Original: CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleID(ModuleID), GetType(PermissionInfo))
    /// Now uses EF Core join with ModulePermissions to find permissions associated with a module instance.
    /// </remarks>
    public async Task<IEnumerable<Permission>> GetByModuleIdAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // Query permissions through ModulePermissions join table
        return await _context.ModulePermissions
            .AsNoTracking()
            .Where(mp => mp.ModuleId == moduleId)
            .Select(mp => mp.Permission)
            .Where(p => p != null)
            .Cast<Permission>()
            .Distinct()
            .OrderBy(p => p.PermissionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET GetPermissionsByFolder(PortalID As Integer, Folder As String) As ArrayList.
    /// Original: CBO.FillCollection(DataProvider.Instance().GetPermissionsByFolderPath(PortalID, Folder), GetType(PermissionInfo))
    /// </para>
    /// <para>
    /// Note: The Folder entity is not included in this migration scope. This method uses raw SQL
    /// to join through the legacy Folders table (which has PortalID and FolderPath columns) to
    /// retrieve permissions associated with a specific folder path within a portal.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<Permission>> GetByFolderAsync(int portalId, string folder, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Use raw SQL because Folder entity is out of scope
        // The Folders table exists in the legacy DNN schema with PortalID and FolderPath columns
        // FolderPermission links to Folders via FolderID
        // Using FormattableString with FromSql for parameterized queries to prevent SQL injection
        return await _context.Permissions
            .FromSql($@"
                SELECT DISTINCT p.PermissionID, p.PermissionCode, p.ModuleDefID, p.PermissionKey, p.PermissionName
                FROM Permissions p
                INNER JOIN FolderPermissions fp ON p.PermissionID = fp.PermissionID
                INNER JOIN Folders f ON fp.FolderID = f.FolderID
                WHERE f.PortalID = {portalId} AND f.FolderPath = {folder}
                ORDER BY p.PermissionID")
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionByCodeAndKey(PermissionCode As String, PermissionKey As String) As ArrayList.
    /// Original: CBO.FillCollection(DataProvider.Instance().GetPermissionByCodeAndKey(PermissionCode, PermissionKey), GetType(PermissionInfo))
    /// Now uses EF Core Where clause with PermissionCode and PermissionKey filtering.
    /// </remarks>
    public async Task<IEnumerable<Permission>> GetByCodeAndKeyAsync(string permissionCode, string permissionKey, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => p.PermissionCode == permissionCode && p.PermissionKey == permissionKey)
            .OrderBy(p => p.PermissionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionsByTabID(TabID As Integer) As ArrayList.
    /// Original: CBO.FillCollection(DataProvider.Instance().GetPermissionsByTabID(TabID), GetType(PermissionInfo))
    /// Now uses EF Core join with TabPermissions to find permissions associated with a tab.
    /// </remarks>
    public async Task<IEnumerable<Permission>> GetByTabIdAsync(int tabId, CancellationToken cancellationToken = default)
    {
        // Query permissions through TabPermissions join table
        return await _context.TabPermissions
            .AsNoTracking()
            .Where(tp => tp.TabId == tabId)
            .Select(tp => tp.Permission)
            .Where(p => p != null)
            .Cast<Permission>()
            .Distinct()
            .OrderBy(p => p.PermissionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET AddPermission(objPermission As PermissionInfo) As Integer.
    /// Original: DataProvider.Instance().AddPermission(objPermission.PermissionCode, objPermission.ModuleDefID,
    ///           objPermission.PermissionKey, objPermission.PermissionName)
    /// </para>
    /// <para>
    /// Now uses EF Core Add + SaveChangesAsync. The PermissionId is automatically populated by EF Core
    /// after the entity is saved to the database (assuming database-generated identity).
    /// </para>
    /// </remarks>
    public async Task<Permission> AddAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return permission;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET UpdatePermission(objPermission As PermissionInfo) As Sub.
    /// Original: DataProvider.Instance().UpdatePermission(objPermission.PermissionID, objPermission.PermissionCode,
    ///           objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName)
    /// </para>
    /// <para>
    /// Now uses EF Core Update + SaveChangesAsync. The entity must have a valid PermissionId set.
    /// </para>
    /// </remarks>
    public async Task UpdateAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        _context.Permissions.Update(permission);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET DeletePermission(permissionID As Integer) As Sub.
    /// Original: DataProvider.Instance().DeletePermission(permissionID)
    /// Now uses EF Core Remove + SaveChangesAsync. First retrieves the entity to ensure it exists.
    /// </remarks>
    public async Task DeleteAsync(int permissionId, CancellationToken cancellationToken = default)
    {
        var permission = await _context.Permissions
            .FirstOrDefaultAsync(p => p.PermissionId == permissionId, cancellationToken)
            .ConfigureAwait(false);

        if (permission is not null)
        {
            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
