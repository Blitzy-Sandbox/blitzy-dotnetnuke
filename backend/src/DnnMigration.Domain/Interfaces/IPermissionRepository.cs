using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository contract for the <see cref="Permission"/> aggregate. Extends the generic
/// <see cref="IRepository{T}"/> CRUD surface with permission lookups mirroring the legacy
/// PermissionController query set.
/// </summary>
public interface IPermissionRepository : IRepository<Permission>
{
    /// <summary>Retrieves the permissions associated with a module.</summary>
    // MIGRATION: legacy PermissionController.GetPermissionsByModuleID(ModuleID)
    // [PermissionController.vb L40] (CBO.FillCollection -> ArrayList of PermissionInfo) -> async collection.
    Task<IEnumerable<Permission>> GetByModuleAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the permissions associated with a tab (page).</summary>
    // MIGRATION: legacy PermissionController.GetPermissionsByTabID(TabID)
    // [PermissionController.vb L54] (CBO.FillCollection -> ArrayList) -> async collection.
    Task<IEnumerable<Permission>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the permissions associated with a folder path in a portal.</summary>
    // MIGRATION: legacy PermissionController.GetPermissionsByFolder(PortalID, Folder)
    // [PermissionController.vb L44] / DataProvider.GetPermissionsByFolderPath(PortalID, Folder)
    // [DataProvider.vb L283] -> async collection.
    Task<IEnumerable<Permission>> GetByFolderAsync(int portalId, string folderPath, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the permissions matching a permission code and key.</summary>
    // MIGRATION: legacy PermissionController.GetPermissionByCodeAndKey(PermissionCode, PermissionKey)
    // [PermissionController.vb L49] (CBO.FillCollection -> ArrayList) -> async collection.
    Task<IEnumerable<Permission>> GetByCodeAndKeyAsync(string permissionCode, string permissionKey, CancellationToken cancellationToken = default);
}
