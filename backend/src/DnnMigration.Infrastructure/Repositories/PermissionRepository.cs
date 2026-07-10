using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPermissionRepository"/> backed by <see cref="DnnDbContext"/>.
/// </summary>
// MIGRATION: Re-expresses the data-access of the legacy VB.NET
// DotNetNuke.Security.Permissions.PermissionController (Library/Components/Security/Permissions/PermissionController.vb)
// as EF Core LINQ. The legacy DataProvider.Instance().<Proc>(...) -> IDataReader -> CBO.FillCollection/
// FillObject pattern is dropped for DnnDbContext DbSet<Permission> queries. The underlying stored procs
// query the base Permission table only (no join to grant tables) and several ignore their parameters;
// those behaviours are preserved verbatim for behavioural equivalence. Data access only.
public class PermissionRepository : IPermissionRepository
{
    private readonly DnnDbContext _context;

    public PermissionRepository(DnnDbContext context)
    {
        _context = context;
    }

    // MIGRATION: PermissionController.GetPermission / DataProvider.GetPermission(permissionId) single reader.
    public async Task<Permission?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PermissionID == id, cancellationToken);
    }

    // MIGRATION: DataProvider.GetPermissions() -> CBO.FillCollection(ArrayList of PermissionInfo).
    public async Task<IEnumerable<Permission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: PermissionController.GetPermissionsByModuleID(ModuleID). Legacy proc:
    //   WHERE ModuleDefID = (SELECT ModuleDefID FROM Modules WHERE ModuleID=@ModuleID)
    //      OR PermissionCode='SYSTEM_MODULE_DEFINITION' ORDER BY PermissionID.
    public async Task<IEnumerable<Permission>> GetByModuleAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: the correlated subquery is a nullable scalar lookup. Casting to (int?) makes an absent
        // module yield null, so "p.ModuleDefID == moduleDefId" then matches nothing (SQL "= NULL" semantics),
        // exactly preserving the legacy stored-proc behaviour.
        int? moduleDefId = await _context.Modules
            .AsNoTracking()
            .Where(m => m.ModuleID == moduleId)
            .Select(m => (int?)m.ModuleDefID)
            .FirstOrDefaultAsync(cancellationToken);

        return await _context.Permissions
            .AsNoTracking()
            .Where(p => p.ModuleDefID == moduleDefId || p.PermissionCode == "SYSTEM_MODULE_DEFINITION")
            .OrderBy(p => p.PermissionID)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: PermissionController.GetPermissionsByTabID(TabID). Legacy proc body:
    //   SELECT ... FROM Permission WHERE PermissionCode='SYSTEM_TAB' ORDER BY PermissionID.
    // The @TabID parameter is IGNORED by the stored procedure; 'tabId' is intentionally unused here to
    // preserve behavioural equivalence (the discard documents the intent and keeps the parameter "used").
    public async Task<IEnumerable<Permission>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default)
    {
        _ = tabId; // MIGRATION: legacy proc ignores this parameter.
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => p.PermissionCode == "SYSTEM_TAB")
            .OrderBy(p => p.PermissionID)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: PermissionController.GetPermissionsByFolder(PortalID, Folder) /
    // DataProvider.GetPermissionsByFolderPath(PortalID, FolderPath). Legacy proc body:
    //   WHERE PermissionCode='SYSTEM_FOLDER' ORDER BY PermissionID.
    // Both @PortalID and @FolderPath are IGNORED by the stored procedure; both parameters are intentionally
    // unused to preserve behavioural equivalence.
    public async Task<IEnumerable<Permission>> GetByFolderAsync(int portalId, string folderPath, CancellationToken cancellationToken = default)
    {
        _ = portalId;   // MIGRATION: legacy proc ignores this parameter.
        _ = folderPath; // MIGRATION: legacy proc ignores this parameter.
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => p.PermissionCode == "SYSTEM_FOLDER")
            .OrderBy(p => p.PermissionID)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: PermissionController.GetPermissionByCodeAndKey(PermissionCode, PermissionKey). Legacy proc:
    //   WHERE (PermissionCode=@code OR @code IS NULL) AND (PermissionKey=@key OR @key IS NULL).
    // The null-tolerant filter is preserved: a null argument matches all rows for that column.
    public async Task<IEnumerable<Permission>> GetByCodeAndKeyAsync(string permissionCode, string permissionKey, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => (permissionCode == null || p.PermissionCode == permissionCode)
                     && (permissionKey == null || p.PermissionKey == permissionKey))
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: PermissionController.AddPermission -> DataProvider.AddPermission stored proc.
    public async Task<Permission> AddAsync(Permission entity, CancellationToken cancellationToken = default)
    {
        _context.Permissions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // MIGRATION: PermissionController.UpdatePermission -> DataProvider.UpdatePermission stored proc.
    public async Task UpdateAsync(Permission entity, CancellationToken cancellationToken = default)
    {
        _context.Permissions.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: PermissionController.DeletePermission -> DataProvider.DeletePermission stored proc. Tracked
    // fetch (no AsNoTracking) so EF can mark the entity Deleted, then remove.
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Permissions
            .FirstOrDefaultAsync(p => p.PermissionID == id, cancellationToken);
        if (entity is not null)
        {
            _context.Permissions.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
