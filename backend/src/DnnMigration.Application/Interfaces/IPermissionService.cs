// -----------------------------------------------------------------------
// <copyright file="IPermissionService.cs" company="DNN Migration Project">
// MIT License - DotNetNuke Migration to .NET 8
// </copyright>
// <summary>
// Application service interface for permission business operations.
// MIGRATION: Derived from VB.NET PermissionController (Library/Components/Security/Permissions/PermissionController.vb).
// Defines async methods for permission CRUD operations and various lookup methods.
// </summary>
// -----------------------------------------------------------------------

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Defines the contract for permission business operations.
/// Provides async methods for permission CRUD operations and various lookup methods
/// by module definition, module, folder, tab, and code/key combinations.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This interface abstracts the permission business logic originally implemented in
/// <c>DotNetNuke.Security.Permissions.PermissionController</c> (Library/Components/Security/Permissions/PermissionController.vb).
/// </para>
/// <para>
/// Key design decisions:
/// <list type="bullet">
/// <item><description>All methods are async with <see cref="CancellationToken"/> support for cooperative cancellation.</description></item>
/// <item><description>Returns DTOs (<see cref="DTOs.PermissionDto"/>) rather than domain entities for API responses.</description></item>
/// <item><description>Uses <see cref="IEnumerable{T}"/> for collection returns to support deferred execution.</description></item>
/// <item><description>Enables dependency injection and unit testing of permission business logic.</description></item>
/// </list>
/// </para>
/// <para>
/// Original VB.NET methods mapped to this interface:
/// <list type="table">
/// <listheader>
/// <term>VB.NET Method</term>
/// <description>C# Interface Method</description>
/// </listheader>
/// <item><term>GetPermission(permissionID As Integer)</term><description><see cref="GetPermissionAsync"/></description></item>
/// <item><term>GetPermissionsByModuleDefID(ModuleDefID As Integer)</term><description><see cref="GetPermissionsByModuleDefinitionIdAsync"/></description></item>
/// <item><term>GetPermissionsByModuleID(ModuleID As Integer)</term><description><see cref="GetPermissionsByModuleIdAsync"/></description></item>
/// <item><term>GetPermissionsByFolder(PortalID As Integer, Folder As String)</term><description><see cref="GetPermissionsByFolderAsync"/></description></item>
/// <item><term>GetPermissionByCodeAndKey(PermissionCode As String, PermissionKey As String)</term><description><see cref="GetPermissionsByCodeAndKeyAsync"/></description></item>
/// <item><term>GetPermissionsByTabID(TabID As Integer)</term><description><see cref="GetPermissionsByTabIdAsync"/></description></item>
/// <item><term>AddPermission(objPermission As PermissionInfo)</term><description><see cref="CreatePermissionAsync"/></description></item>
/// <item><term>UpdatePermission(objPermission As PermissionInfo)</term><description><see cref="UpdatePermissionAsync"/></description></item>
/// <item><term>DeletePermission(permissionID As Integer)</term><description><see cref="DeletePermissionAsync"/></description></item>
/// </list>
/// </para>
/// </remarks>
public interface IPermissionService
{
    /// <summary>
    /// Retrieves a permission by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the permission to retrieve.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// <see cref="DTOs.PermissionDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermission(ByVal permissionID As Integer) As PermissionInfo</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 30).
    /// </para>
    /// <para>
    /// The original implementation used <c>CBO.FillObject(DataProvider.Instance().GetPermission(permissionID), GetType(PermissionInfo))</c>
    /// to retrieve and hydrate the permission entity from the data provider.
    /// </para>
    /// </remarks>
    Task<DTOs.PermissionDto?> GetPermissionAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions associated with a specific module definition.
    /// </summary>
    /// <param name="moduleDefId">The module definition identifier to filter permissions by.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="DTOs.PermissionDto"/> objects associated with the module definition.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionsByModuleDefID(ByVal ModuleDefID As Integer) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 34).
    /// </para>
    /// <para>
    /// The original implementation used <c>CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleDefID(ModuleDefID), GetType(PermissionInfo))</c>
    /// to retrieve all permissions defined for a specific module definition type.
    /// Module definitions define permission sets that apply to all instances of that module type.
    /// </para>
    /// </remarks>
    Task<IEnumerable<DTOs.PermissionDto>> GetPermissionsByModuleDefinitionIdAsync(
        int moduleDefId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions associated with a specific module instance.
    /// </summary>
    /// <param name="moduleId">The module instance identifier to filter permissions by.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="DTOs.PermissionDto"/> objects associated with the module instance.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionsByModuleID(ByVal ModuleID As Integer) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 38).
    /// </para>
    /// <para>
    /// The original implementation used <c>CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleID(ModuleID), GetType(PermissionInfo))</c>
    /// to retrieve permissions assigned to a specific module instance on a page.
    /// This includes both inherited permissions from the module definition and instance-specific permissions.
    /// </para>
    /// </remarks>
    Task<IEnumerable<DTOs.PermissionDto>> GetPermissionsByModuleIdAsync(
        int moduleId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions associated with a specific folder within a portal.
    /// </summary>
    /// <param name="portalId">The portal identifier containing the folder.</param>
    /// <param name="folder">The folder path to filter permissions by.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="DTOs.PermissionDto"/> objects associated with the folder.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionsByFolder(ByVal PortalID As Integer, ByVal Folder As String) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 43).
    /// </para>
    /// <para>
    /// The original implementation used <c>CBO.FillCollection(DataProvider.Instance().GetPermissionsByFolderPath(PortalID, Folder), GetType(PermissionInfo))</c>
    /// to retrieve folder-level permissions. Folder permissions control access to files and subfolders
    /// within the portal's file system.
    /// </para>
    /// </remarks>
    Task<IEnumerable<DTOs.PermissionDto>> GetPermissionsByFolderAsync(
        int portalId, 
        string folder, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions matching a specific permission code and key combination.
    /// </summary>
    /// <param name="permissionCode">The permission code to filter by (e.g., "SYSTEM_TAB", "CONTENT_MODULE").</param>
    /// <param name="permissionKey">The permission key to filter by (e.g., "VIEW", "EDIT", "DELETE").</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="DTOs.PermissionDto"/> objects matching the code and key.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionByCodeAndKey(ByVal PermissionCode As String, ByVal PermissionKey As String) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 47).
    /// </para>
    /// <para>
    /// The original implementation used <c>CBO.FillCollection(DataProvider.Instance().GetPermissionByCodeAndKey(PermissionCode, PermissionKey), GetType(PermissionInfo))</c>
    /// to retrieve permissions by their code and key. Permission codes categorize permissions (e.g., "SYSTEM_TAB" for tab permissions),
    /// while permission keys identify specific actions within that category (e.g., "VIEW", "EDIT").
    /// </para>
    /// </remarks>
    Task<IEnumerable<DTOs.PermissionDto>> GetPermissionsByCodeAndKeyAsync(
        string permissionCode, 
        string permissionKey, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions associated with a specific tab (page).
    /// </summary>
    /// <param name="tabId">The tab identifier to filter permissions by.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="DTOs.PermissionDto"/> objects associated with the tab.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionsByTabID(ByVal TabID As Integer) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 51).
    /// </para>
    /// <para>
    /// The original implementation used <c>CBO.FillCollection(DataProvider.Instance().GetPermissionsByTabID(TabID), GetType(PermissionInfo))</c>
    /// to retrieve tab-level permissions. Tab permissions control access to pages and their content,
    /// including view, edit, and administrative access rights.
    /// </para>
    /// </remarks>
    Task<IEnumerable<DTOs.PermissionDto>> GetPermissionsByTabIdAsync(
        int tabId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new permission based on the provided request data.
    /// </summary>
    /// <param name="request">
    /// A <see cref="DTOs.CreatePermissionRequest"/> containing the permission creation parameters including
    /// permission code, module definition ID, permission key, and permission name.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// newly created <see cref="DTOs.PermissionDto"/> with the assigned permission ID.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when permission creation fails due to validation errors or duplicate permission definition.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.AddPermission(ByVal objPermission As PermissionInfo) As Integer</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 59).
    /// </para>
    /// <para>
    /// The original implementation called <c>DataProvider.Instance().AddPermission(objPermission.PermissionCode, 
    /// objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName)</c>
    /// to create a new permission definition in the database.
    /// </para>
    /// </remarks>
    Task<DTOs.PermissionDto> CreatePermissionAsync(
        DTOs.CreatePermissionRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing permission with the provided settings.
    /// </summary>
    /// <param name="id">The unique identifier of the permission to update.</param>
    /// <param name="request">
    /// An <see cref="DTOs.UpdatePermissionRequest"/> containing the updated permission settings.
    /// All properties are optional to support partial updates.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// updated <see cref="DTOs.PermissionDto"/> reflecting all applied changes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the permission with the specified <paramref name="id"/> does not exist
    /// or when update fails due to validation errors.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.UpdatePermission(ByVal objPermission As PermissionInfo)</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 63).
    /// </para>
    /// <para>
    /// The original implementation called <c>DataProvider.Instance().UpdatePermission(objPermission.PermissionID, 
    /// objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName)</c>
    /// to update the permission definition in the database.
    /// </para>
    /// </remarks>
    Task<DTOs.PermissionDto> UpdatePermissionAsync(
        int id, 
        DTOs.UpdatePermissionRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a permission by its unique identifier.
    /// </summary>
    /// <param name="permissionId">The unique identifier of the permission to delete.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous delete operation.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the permission cannot be deleted (e.g., it is in use by module or tab permissions
    /// or deletion is otherwise prohibited by business rules).
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.DeletePermission(ByVal permissionID As Integer)</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 55).
    /// </para>
    /// <para>
    /// The original implementation called <c>DataProvider.Instance().DeletePermission(permissionID)</c>
    /// to remove the permission definition from the database. Care should be taken when deleting
    /// permissions as it may affect existing role and user permission assignments.
    /// </para>
    /// </remarks>
    Task DeletePermissionAsync(int permissionId, CancellationToken cancellationToken = default);
}
