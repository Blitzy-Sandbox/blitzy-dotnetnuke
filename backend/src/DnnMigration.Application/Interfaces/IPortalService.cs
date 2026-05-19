// -----------------------------------------------------------------------
// <copyright file="IPortalService.cs" company="DNN Migration Project">
// MIT License - DotNetNuke Migration to .NET 8
// </copyright>
// <summary>
// Application service interface for portal business operations.
// MIGRATION: Derived from VB.NET PortalController (Library/Components/Portal/PortalController.vb).
// Defines async methods for portal CRUD operations and storage quota management.
// </summary>
// -----------------------------------------------------------------------

using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Defines the contract for portal business operations.
/// Provides async methods for portal CRUD operations, search functionality,
/// and storage quota management.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This interface abstracts the portal business logic originally implemented in
/// <c>DotNetNuke.Entities.Portals.PortalController</c> (Library/Components/Portal/PortalController.vb).
/// </para>
/// <para>
/// Key design decisions:
/// <list type="bullet">
/// <item><description>All methods are async with <see cref="CancellationToken"/> support for cooperative cancellation.</description></item>
/// <item><description>Returns DTOs (<see cref="PortalDto"/>) rather than domain entities for API responses.</description></item>
/// <item><description>Uses <see cref="PagedResult{T}"/> for paginated list operations.</description></item>
/// <item><description>Enables dependency injection and unit testing of portal business logic.</description></item>
/// </list>
/// </para>
/// <para>
/// Original VB.NET methods mapped to this interface:
/// <list type="table">
/// <listheader>
/// <term>VB.NET Method</term>
/// <description>C# Interface Method</description>
/// </listheader>
/// <item><term>GetPortal(PortalId As Integer)</term><description><see cref="GetPortalAsync"/></description></item>
/// <item><term>GetPortals()</term><description><see cref="GetPortalsAsync"/></description></item>
/// <item><term>GetPortalsByName(nameToMatch, pageIndex, pageSize, totalRecords)</term><description><see cref="GetPortalsByNameAsync"/></description></item>
/// <item><term>CreatePortal(...) called by Signup.ascx.vb</term><description><see cref="CreatePortalAsync"/></description></item>
/// <item><term>UpdatePortalInfo(...)</term><description><see cref="UpdatePortalAsync"/></description></item>
/// <item><term>DeletePortalInfo(PortalId)</term><description><see cref="DeletePortalAsync"/></description></item>
/// <item><term>GetPortalSpaceUsedBytes(portalId)</term><description><see cref="GetPortalSpaceUsedAsync"/></description></item>
/// <item><term>HasSpaceAvailable(portalId, fileSizeBytes)</term><description><see cref="HasSpaceAvailableAsync"/></description></item>
/// </list>
/// </para>
/// </remarks>
public interface IPortalService
{
    /// <summary>
    /// Retrieves a portal by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the portal to retrieve.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// <see cref="PortalDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from <c>PortalController.GetPortal(ByVal PortalId As Integer) As PortalInfo</c>
    /// (Library/Components/Portal/PortalController.vb, line 1224).
    /// The original method used caching via DataCache.GetPersistentCacheItem; caching strategy
    /// should be implemented in the service implementation or infrastructure layer.
    /// </remarks>
    Task<PortalDto?> GetPortalAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of all portals.
    /// </summary>
    /// <param name="pageIndex">The zero-based index of the page to retrieve.</param>
    /// <param name="pageSize">The maximum number of portals per page.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a
    /// <see cref="PagedResult{T}"/> of <see cref="PortalDto"/> objects.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from <c>PortalController.GetPortals() As ArrayList</c>
    /// (Library/Components/Portal/PortalController.vb, line 1263).
    /// The original method returned all portals without pagination; this interface
    /// adds pagination support for better performance with large portal counts.
    /// </remarks>
    Task<PagedResult<PortalDto>> GetPortalsAsync(
        int pageIndex, 
        int pageSize, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of portals whose names match the specified filter expression.
    /// </summary>
    /// <param name="nameToMatch">
    /// The filter expression to match against portal names. Supports SQL LIKE pattern matching
    /// (e.g., "%portal%" to match any portal containing "portal" in its name).
    /// </param>
    /// <param name="pageIndex">The zero-based index of the page to retrieve.</param>
    /// <param name="pageSize">The maximum number of portals per page.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a
    /// <see cref="PagedResult{T}"/> of <see cref="PortalDto"/> objects matching the filter.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PortalController.GetPortalsByName(ByVal nameToMatch As String, 
    /// ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList</c>
    /// (Library/Components/Portal/PortalController.vb, line 262).
    /// </para>
    /// <para>
    /// The original VB.NET implementation handled special case where pageIndex = -1 by
    /// returning all records (pageSize = Integer.MaxValue). This behavior is preserved
    /// in the service implementation.
    /// </para>
    /// </remarks>
    Task<PagedResult<PortalDto>> GetPortalsByNameAsync(
        string nameToMatch, 
        int pageIndex, 
        int pageSize, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new portal based on the provided request data.
    /// </summary>
    /// <param name="request">
    /// A <see cref="CreatePortalRequest"/> containing the portal creation parameters including
    /// portal alias, title, administrator credentials, template selection, and other settings.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// newly created <see cref="PortalDto"/> with the assigned portal ID and all properties.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when portal creation fails due to validation errors, duplicate alias, or other business rule violations.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from the portal creation workflow in <c>Signup.ascx.vb</c> cmdUpdate_Click handler
    /// (Website/admin/Portal/Signup.ascx.vb, line 274) which calls <c>PortalController.CreatePortal</c>
    /// (Library/Components/Portal/PortalController.vb, line 326).
    /// </para>
    /// <para>
    /// The creation process typically involves:
    /// <list type="number">
    /// <item><description>Creating the portal database record with default settings from host configuration.</description></item>
    /// <item><description>Creating the portal administrator user account.</description></item>
    /// <item><description>Setting up default roles (Administrators, Registered Users).</description></item>
    /// <item><description>Processing the portal template to create initial pages and modules.</description></item>
    /// <item><description>Creating the portal home directory structure.</description></item>
    /// <item><description>Registering the portal alias.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    Task<PortalDto> CreatePortalAsync(
        CreatePortalRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing portal with the provided settings.
    /// </summary>
    /// <param name="id">The unique identifier of the portal to update.</param>
    /// <param name="request">
    /// An <see cref="UpdatePortalRequest"/> containing the updated portal settings including
    /// branding, quotas, navigation tabs, and localization configuration.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// updated <see cref="PortalDto"/> reflecting all applied changes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the portal with the specified <paramref name="id"/> does not exist
    /// or when update fails due to validation errors.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PortalController.UpdatePortalInfo(...)</c>
    /// (Library/Components/Portal/PortalController.vb, line 1568).
    /// </para>
    /// <para>
    /// The original VB.NET method accepted 27 individual parameters; these are now
    /// encapsulated in the <see cref="UpdatePortalRequest"/> record for cleaner API design.
    /// After update, the portal cache is cleared to ensure subsequent reads reflect the changes.
    /// </para>
    /// </remarks>
    Task<PortalDto> UpdatePortalAsync(
        int id, 
        UpdatePortalRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a portal and all associated data.
    /// </summary>
    /// <param name="id">The unique identifier of the portal to delete.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous delete operation.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the portal cannot be deleted (e.g., it is the last remaining portal
    /// or deletion is otherwise prohibited by business rules).
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PortalController.DeletePortal(portal As PortalInfo, serverPath As String) As String</c>
    /// (Library/Components/Portal/PortalController.vb, line 162) and 
    /// <c>PortalController.DeletePortalInfo(PortalId As Integer)</c> (line 1201).
    /// </para>
    /// <para>
    /// The deletion process includes:
    /// <list type="number">
    /// <item><description>Verifying this is not the last portal (at least one portal must remain).</description></item>
    /// <item><description>Deleting custom resource files (.Portal-[PortalID].resx).</description></item>
    /// <item><description>Deleting child portal directory if applicable.</description></item>
    /// <item><description>Deleting the portal upload directory (Portals\[PortalID]).</description></item>
    /// <item><description>Deleting the portal home directory.</description></item>
    /// <item><description>Removing all database references.</description></item>
    /// <item><description>Clearing portal alias cache.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    Task DeletePortalAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total disk space used by a portal in bytes.
    /// </summary>
    /// <param name="portalId">
    /// The unique identifier of the portal. Use -1 to get the host-level space
    /// (Portals\_default directory).
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// total space used by the portal in bytes.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PortalController.GetPortalSpaceUsedBytes(ByVal portalId As Integer) As Long</c>
    /// (Library/Components/Portal/PortalController.vb, line 1296).
    /// </para>
    /// <para>
    /// The original implementation queries the database for file sizes stored in the Files table,
    /// which accounts for files stored using secure file storage options.
    /// This replaces the earlier obsolete <c>GetPortalSpaceUsed</c> method which returned Integer
    /// and had overflow issues with portals larger than 2GB.
    /// </para>
    /// </remarks>
    Task<long> GetPortalSpaceUsedAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether there is sufficient disk space available to upload a file of the specified size.
    /// </summary>
    /// <param name="portalId">
    /// The unique identifier of the portal to check. Use -1 for host-level storage check.
    /// </param>
    /// <param name="fileSizeBytes">
    /// The size of the file to be uploaded, in bytes.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <c>true</c> if
    /// there is sufficient space to upload the file; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from <c>PortalController.HasSpaceAvailable(ByVal portalId As Integer, 
    /// ByVal fileSizeBytes As Long) As Boolean</c>
    /// (Library/Components/Portal/PortalController.vb, line 1323).
    /// </para>
    /// <para>
    /// The space check logic:
    /// <list type="bullet">
    /// <item><description>For host level (portalId = -1): Always returns <c>true</c> (no limit).</description></item>
    /// <item><description>For portals with HostSpace = 0: Returns <c>true</c> (unlimited storage).</description></item>
    /// <item><description>For portals with HostSpace &gt; 0: Returns <c>true</c> if 
    /// (currentUsage + fileSizeBytes) / (1024²) ≤ HostSpace in MB.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    Task<bool> HasSpaceAvailableAsync(
        int portalId, 
        long fileSizeBytes, 
        CancellationToken cancellationToken = default);
}
