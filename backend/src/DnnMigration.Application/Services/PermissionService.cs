// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Application service implementing permission business logic from PermissionController.vb.
// Source: Library/Components/Security/Permissions/PermissionController.vb
// Changes:
// - Converted from VB.NET PermissionController public class to C# 12 service class
// - Implemented IPermissionService interface with dependency injection
// - Replaced DataProvider.Instance() calls with IPermissionRepository dependency
// - Replaced CBO.FillObject/CBO.FillCollection with AutoMapper entity-to-DTO mapping
// - Converted synchronous methods to async Task-based patterns with CancellationToken support
// - Applied ConfigureAwait(false) per library code patterns (Section 0.7.2)
// - Used file-scoped namespace per C# 12 conventions
// - Added comprehensive XML documentation comments
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing permission management business logic.
/// Orchestrates permission CRUD operations between the API layer and domain/repository layers.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This service extracts business logic exactly as implemented in the original
/// VB.NET <c>DotNetNuke.Security.Permissions.PermissionController</c> class
/// (Library/Components/Security/Permissions/PermissionController.vb).
/// </para>
/// <para>
/// Key architectural changes from the original:
/// <list type="bullet">
/// <item><description>Dependency Injection: Uses constructor injection for <see cref="IPermissionRepository"/> and <see cref="IMapper"/> instead of static DataProvider.Instance() calls.</description></item>
/// <item><description>Async/Await: All methods are asynchronous with <see cref="CancellationToken"/> support for cooperative cancellation.</description></item>
/// <item><description>DTO Mapping: Uses AutoMapper for entity-to-DTO transformations instead of CBO.FillObject/CBO.FillCollection.</description></item>
/// <item><description>Interface Abstraction: Implements <see cref="IPermissionService"/> for testability and DI container registration.</description></item>
/// </list>
/// </para>
/// <para>
/// Original VB.NET method mappings:
/// <list type="table">
/// <listheader>
/// <term>VB.NET Method</term>
/// <description>C# Service Method</description>
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
public class PermissionService : IPermissionService
{
    /// <summary>
    /// The repository providing data access for Permission entities.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Replaces static DataProvider.Instance() calls from original VB.NET implementation.
    /// </remarks>
    private readonly IPermissionRepository _permissionRepository;

    /// <summary>
    /// The AutoMapper instance for entity-to-DTO transformations.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Replaces CBO.FillObject and CBO.FillCollection from original VB.NET implementation.
    /// </remarks>
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionService"/> class.
    /// </summary>
    /// <param name="permissionRepository">
    /// The repository providing data access for Permission entities.
    /// </param>
    /// <param name="mapper">
    /// The AutoMapper instance for entity-to-DTO transformations.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="permissionRepository"/> or <paramref name="mapper"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// MIGRATION: Constructor injection replaces static DataProvider.Instance() pattern from VB.NET.
    /// Dependencies are registered in the DI container and resolved at runtime.
    /// </remarks>
    public PermissionService(IPermissionRepository permissionRepository, IMapper mapper)
    {
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Retrieves a permission by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the permission to retrieve.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// <see cref="PermissionDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermission(ByVal permissionID As Integer) As PermissionInfo</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 30-32).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Return CType(CBO.FillObject(DataProvider.Instance().GetPermission(permissionID), GetType(PermissionInfo)), PermissionInfo)
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<PermissionDto?> GetPermissionAsync(int id, CancellationToken cancellationToken = default)
    {
        // MIGRATION: DataProvider.Instance().GetPermission(permissionID) → _permissionRepository.GetByIdAsync(id)
        var permission = await _permissionRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        // MIGRATION: CBO.FillObject → AutoMapper IMapper.Map<PermissionDto>
        // Returns null if permission not found, matching original VB.NET behavior where CType returns Nothing
        return permission is null ? null : _mapper.Map<PermissionDto>(permission);
    }

    /// <summary>
    /// Retrieves all permissions associated with a specific module definition.
    /// </summary>
    /// <param name="moduleDefId">The module definition identifier to filter permissions by.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="PermissionDto"/> objects associated with the module definition.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionsByModuleDefID(ByVal ModuleDefID As Integer) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 34-36).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleDefID(ModuleDefID), GetType(PermissionInfo))
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<PermissionDto>> GetPermissionsByModuleDefinitionIdAsync(
        int moduleDefId, 
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: DataProvider.Instance().GetPermissionsByModuleDefID(ModuleDefID) → _permissionRepository.GetByModuleDefIdAsync(moduleDefId)
        var permissions = await _permissionRepository.GetByModuleDefIdAsync(moduleDefId, cancellationToken).ConfigureAwait(false);

        // MIGRATION: CBO.FillCollection → AutoMapper IMapper.Map<IEnumerable<PermissionDto>>
        // MIGRATION: Replaced VB.NET For Each loops with LINQ Select projection
        return _mapper.Map<IEnumerable<PermissionDto>>(permissions);
    }

    /// <summary>
    /// Retrieves all permissions associated with a specific module instance.
    /// </summary>
    /// <param name="moduleId">The module instance identifier to filter permissions by.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="PermissionDto"/> objects associated with the module instance.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionsByModuleID(ByVal ModuleID As Integer) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 38-40).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleID(ModuleID), GetType(PermissionInfo))
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<PermissionDto>> GetPermissionsByModuleIdAsync(
        int moduleId, 
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: DataProvider.Instance().GetPermissionsByModuleID(ModuleID) → _permissionRepository.GetByModuleIdAsync(moduleId)
        var permissions = await _permissionRepository.GetByModuleIdAsync(moduleId, cancellationToken).ConfigureAwait(false);

        // MIGRATION: CBO.FillCollection → AutoMapper IMapper.Map<IEnumerable<PermissionDto>>
        return _mapper.Map<IEnumerable<PermissionDto>>(permissions);
    }

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
    /// <see cref="IEnumerable{T}"/> of <see cref="PermissionDto"/> objects associated with the folder.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="folder"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionsByFolder(ByVal PortalID As Integer, ByVal Folder As String) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 43-45).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByFolderPath(PortalID, Folder), GetType(PermissionInfo))
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<PermissionDto>> GetPermissionsByFolderAsync(
        int portalId, 
        string folder, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);

        // MIGRATION: DataProvider.Instance().GetPermissionsByFolderPath(PortalID, Folder) → _permissionRepository.GetByFolderAsync(portalId, folder)
        var permissions = await _permissionRepository.GetByFolderAsync(portalId, folder, cancellationToken).ConfigureAwait(false);

        // MIGRATION: CBO.FillCollection → AutoMapper IMapper.Map<IEnumerable<PermissionDto>>
        return _mapper.Map<IEnumerable<PermissionDto>>(permissions);
    }

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
    /// <see cref="IEnumerable{T}"/> of <see cref="PermissionDto"/> objects matching the code and key.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="permissionCode"/> or <paramref name="permissionKey"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionByCodeAndKey(ByVal PermissionCode As String, ByVal PermissionKey As String) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 47-49).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionByCodeAndKey(PermissionCode, PermissionKey), GetType(PermissionInfo))
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<PermissionDto>> GetPermissionsByCodeAndKeyAsync(
        string permissionCode, 
        string permissionKey, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissionCode);
        ArgumentNullException.ThrowIfNull(permissionKey);

        // MIGRATION: DataProvider.Instance().GetPermissionByCodeAndKey(PermissionCode, PermissionKey) → _permissionRepository.GetByCodeAndKeyAsync(permissionCode, permissionKey)
        var permissions = await _permissionRepository.GetByCodeAndKeyAsync(permissionCode, permissionKey, cancellationToken).ConfigureAwait(false);

        // MIGRATION: CBO.FillCollection → AutoMapper IMapper.Map<IEnumerable<PermissionDto>>
        return _mapper.Map<IEnumerable<PermissionDto>>(permissions);
    }

    /// <summary>
    /// Retrieves all permissions associated with a specific tab (page).
    /// </summary>
    /// <param name="tabId">The tab identifier to filter permissions by.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="IEnumerable{T}"/> of <see cref="PermissionDto"/> objects associated with the tab.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.GetPermissionsByTabID(ByVal TabID As Integer) As ArrayList</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 51-53).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByTabID(TabID), GetType(PermissionInfo))
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<PermissionDto>> GetPermissionsByTabIdAsync(
        int tabId, 
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: DataProvider.Instance().GetPermissionsByTabID(TabID) → _permissionRepository.GetByTabIdAsync(tabId)
        var permissions = await _permissionRepository.GetByTabIdAsync(tabId, cancellationToken).ConfigureAwait(false);

        // MIGRATION: CBO.FillCollection → AutoMapper IMapper.Map<IEnumerable<PermissionDto>>
        return _mapper.Map<IEnumerable<PermissionDto>>(permissions);
    }

    /// <summary>
    /// Creates a new permission based on the provided request data.
    /// </summary>
    /// <param name="request">
    /// A <see cref="CreatePermissionRequest"/> containing the permission creation parameters including
    /// permission code, module definition ID, permission key, and permission name.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// newly created <see cref="PermissionDto"/> with the assigned permission ID.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.AddPermission(ByVal objPermission As PermissionInfo) As Integer</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 59-61).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Return CType(DataProvider.Instance().AddPermission(objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName), Integer)
    /// </code>
    /// </para>
    /// <para>
    /// The original method returned only the new permission ID as an integer. This implementation
    /// returns the complete <see cref="PermissionDto"/> for richer API responses.
    /// </para>
    /// </remarks>
    public async Task<PermissionDto> CreatePermissionAsync(
        CreatePermissionRequest request, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: Map CreatePermissionRequest to Permission entity
        // Original: objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName
        var permission = new Permission
        {
            PermissionCode = request.PermissionCode,
            ModuleDefId = request.ModuleDefId,
            PermissionKey = request.PermissionKey,
            PermissionName = request.PermissionName
        };

        // MIGRATION: DataProvider.Instance().AddPermission(...) → _permissionRepository.AddAsync(permission)
        var createdPermission = await _permissionRepository.AddAsync(permission, cancellationToken).ConfigureAwait(false);

        // MIGRATION: Original returned Integer (permission ID). Now returning full PermissionDto for API response.
        return _mapper.Map<PermissionDto>(createdPermission);
    }

    /// <summary>
    /// Updates an existing permission with the provided settings.
    /// </summary>
    /// <param name="id">The unique identifier of the permission to update.</param>
    /// <param name="request">
    /// An <see cref="UpdatePermissionRequest"/> containing the updated permission settings.
    /// All properties are optional to support partial updates.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// updated <see cref="PermissionDto"/> reflecting all applied changes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the permission with the specified <paramref name="id"/> does not exist.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.UpdatePermission(ByVal objPermission As PermissionInfo)</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 63-65).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// DataProvider.Instance().UpdatePermission(objPermission.PermissionID, objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName)
    /// </code>
    /// </para>
    /// <para>
    /// This implementation supports partial updates (PATCH semantics) by only applying non-null
    /// values from the request. The original VB.NET implementation always updated all fields.
    /// </para>
    /// </remarks>
    public async Task<PermissionDto> UpdatePermissionAsync(
        int id, 
        UpdatePermissionRequest request, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: First retrieve existing permission (original VB.NET assumed valid ID)
        var existingPermission = await _permissionRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        
        if (existingPermission is null)
        {
            throw new InvalidOperationException($"Permission with ID {id} not found.");
        }

        // MIGRATION: Apply partial updates from request (only non-null values)
        // Original: objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName
        if (request.PermissionCode is not null)
        {
            existingPermission.PermissionCode = request.PermissionCode;
        }

        if (request.ModuleDefId.HasValue)
        {
            existingPermission.ModuleDefId = request.ModuleDefId.Value;
        }

        if (request.PermissionKey is not null)
        {
            existingPermission.PermissionKey = request.PermissionKey;
        }

        if (request.PermissionName is not null)
        {
            existingPermission.PermissionName = request.PermissionName;
        }

        // MIGRATION: DataProvider.Instance().UpdatePermission(...) → _permissionRepository.UpdateAsync(existingPermission)
        await _permissionRepository.UpdateAsync(existingPermission, cancellationToken).ConfigureAwait(false);

        // MIGRATION: Original was Sub (void). Now returning PermissionDto for API response.
        return _mapper.Map<PermissionDto>(existingPermission);
    }

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
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PermissionController.DeletePermission(ByVal permissionID As Integer)</c>
    /// (Library/Components/Security/Permissions/PermissionController.vb, line 55-57).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// DataProvider.Instance().DeletePermission(permissionID)
    /// </code>
    /// </para>
    /// <para>
    /// Business logic preserved exactly: This method deletes the permission definition from the database.
    /// Care should be taken when deleting permissions as it may affect existing role and user 
    /// permission assignments that reference this permission definition.
    /// </para>
    /// </remarks>
    public async Task DeletePermissionAsync(int permissionId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: DataProvider.Instance().DeletePermission(permissionID) → _permissionRepository.DeleteAsync(permissionId)
        await _permissionRepository.DeleteAsync(permissionId, cancellationToken).ConfigureAwait(false);
    }
}
