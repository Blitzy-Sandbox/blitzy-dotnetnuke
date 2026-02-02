// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Repository interface extracted from DataProvider.Instance() calls in PermissionController.vb
// Source: Library/Components/Security/Permissions/PermissionController.vb
// Changes:
// - Extracted data access contract from VB.NET PermissionController class
// - Converted synchronous methods to async Task-based patterns with CancellationToken support
// - Changed VB.NET ArrayList returns to IEnumerable<Permission> for type safety
// - Applied nullable reference types for GetByIdAsync return type
// - Used file-scoped namespace per C# 12 conventions
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository interface defining data access contract for Permission entities.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the contract for permission CRUD operations, enabling the
/// Infrastructure layer to implement data access while maintaining domain layer independence.
/// All methods support async/await patterns with <see cref="CancellationToken"/> for
/// cooperative cancellation of long-running database operations.
/// </para>
/// <para>
/// MIGRATION: Extracted from legacy PermissionController.vb permission management operations
/// including GetPermission, GetPermissionsByModuleDefID, GetPermissionsByModuleID,
/// GetPermissionsByFolder, GetPermissionByCodeAndKey, GetPermissionsByTabID, AddPermission,
/// UpdatePermission, and DeletePermission.
/// </para>
/// </remarks>
public interface IPermissionRepository
{
    /// <summary>
    /// Retrieves a permission by its unique identifier.
    /// </summary>
    /// <param name="permissionId">The unique identifier of the permission to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// <see cref="Permission"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermission(permissionID As Integer) As PermissionInfo.
    /// Original implementation: DataProvider.Instance().GetPermission(permissionID)
    /// </remarks>
    Task<Permission?> GetByIdAsync(int permissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions associated with a specific module definition.
    /// </summary>
    /// <param name="moduleDefId">The module definition identifier to filter permissions by.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="Permission"/> entities matching the
    /// module definition ID.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionsByModuleDefID(ModuleDefID As Integer) As ArrayList.
    /// Original implementation: DataProvider.Instance().GetPermissionsByModuleDefID(ModuleDefID)
    /// </remarks>
    Task<IEnumerable<Permission>> GetByModuleDefIdAsync(int moduleDefId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions associated with a specific module instance.
    /// </summary>
    /// <param name="moduleId">The module instance identifier to filter permissions by.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="Permission"/> entities matching the
    /// module ID.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionsByModuleID(ModuleID As Integer) As ArrayList.
    /// Original implementation: DataProvider.Instance().GetPermissionsByModuleID(ModuleID)
    /// </remarks>
    Task<IEnumerable<Permission>> GetByModuleIdAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions associated with a specific folder within a portal.
    /// </summary>
    /// <param name="portalId">The portal identifier containing the folder.</param>
    /// <param name="folder">The folder path to filter permissions by.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="Permission"/> entities matching the
    /// portal ID and folder path.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionsByFolder(PortalID As Integer, Folder As String) As ArrayList.
    /// Original implementation: DataProvider.Instance().GetPermissionsByFolderPath(PortalID, Folder)
    /// </remarks>
    Task<IEnumerable<Permission>> GetByFolderAsync(int portalId, string folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions matching a specific permission code and key combination.
    /// </summary>
    /// <param name="permissionCode">The permission code to filter by (e.g., "SYSTEM_MODULE_DEFINITION").</param>
    /// <param name="permissionKey">The permission key to filter by (e.g., "VIEW", "EDIT").</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="Permission"/> entities matching the
    /// permission code and key.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionByCodeAndKey(PermissionCode As String, PermissionKey As String) As ArrayList.
    /// Original implementation: DataProvider.Instance().GetPermissionByCodeAndKey(PermissionCode, PermissionKey)
    /// </remarks>
    Task<IEnumerable<Permission>> GetByCodeAndKeyAsync(string permissionCode, string permissionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions associated with a specific tab (page).
    /// </summary>
    /// <param name="tabId">The tab identifier to filter permissions by.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="Permission"/> entities matching the
    /// tab ID.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET GetPermissionsByTabID(TabID As Integer) As ArrayList.
    /// Original implementation: DataProvider.Instance().GetPermissionsByTabID(TabID)
    /// </remarks>
    Task<IEnumerable<Permission>> GetByTabIdAsync(int tabId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new permission to the data store.
    /// </summary>
    /// <param name="permission">The permission entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// newly created <see cref="Permission"/> entity with its assigned identifier.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET AddPermission(objPermission As PermissionInfo) As Integer.
    /// Original implementation: DataProvider.Instance().AddPermission(objPermission.PermissionCode,
    /// objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName)
    /// </para>
    /// <para>
    /// The original method returned the new permission ID as an integer. This method returns
    /// the complete Permission entity with the PermissionId property populated.
    /// </para>
    /// </remarks>
    Task<Permission> AddAsync(Permission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing permission in the data store.
    /// </summary>
    /// <param name="permission">The permission entity with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET UpdatePermission(objPermission As PermissionInfo) As Sub.
    /// Original implementation: DataProvider.Instance().UpdatePermission(objPermission.PermissionID,
    /// objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey,
    /// objPermission.PermissionName)
    /// </para>
    /// <para>
    /// The permission entity's <see cref="Permission.PermissionId"/> is used to identify
    /// the record to update. The <see cref="Permission.PermissionCode"/>, 
    /// <see cref="Permission.ModuleDefId"/>, <see cref="Permission.PermissionKey"/>, and
    /// <see cref="Permission.PermissionName"/> properties contain the updated values.
    /// </para>
    /// </remarks>
    Task UpdateAsync(Permission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a permission from the data store by its unique identifier.
    /// </summary>
    /// <param name="permissionId">The unique identifier of the permission to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET DeletePermission(permissionID As Integer) As Sub.
    /// Original implementation: DataProvider.Instance().DeletePermission(permissionID)
    /// </remarks>
    Task DeleteAsync(int permissionId, CancellationToken cancellationToken = default);
}
