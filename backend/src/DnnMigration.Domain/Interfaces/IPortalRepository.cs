// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Portals.PortalController → C# 12 IPortalRepository interface
// Source: Library/Components/Portal/PortalController.vb
// Changes:
// - Extracted interface from VB.NET PortalController class
// - GetPortal → GetByIdAsync with nullable return type
// - GetPortals → GetAllAsync with IEnumerable return
// - GetPortalsByName → GetByNameAsync and GetPagedAsync for pagination
// - GetExpiredPortals → GetExpiredAsync
// - CreatePortal → AddAsync returning the created entity
// - UpdatePortalInfo → UpdateAsync
// - DeletePortalInfo → DeleteAsync
// - Added GetPortalCountAsync for existence validation
// - Added ExistsAsync for portal existence checks
// - All methods use async/await with Task return types
// - All methods include CancellationToken with default value
// - Uses file-scoped namespace per C# 12 standards
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository interface defining data access contract for Portal entities.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts data access operations for the Portal entity,
/// enabling the Infrastructure layer to implement data access while maintaining
/// domain layer independence.
/// </para>
/// <para>
/// MIGRATION: Extracted from legacy VB.NET PortalController.vb CRUD operations:
/// <list type="bullet">
///   <item><description>GetPortal → <see cref="GetByIdAsync"/></description></item>
///   <item><description>GetPortals → <see cref="GetAllAsync"/></description></item>
///   <item><description>GetPortalsByName → <see cref="GetByNameAsync"/>, <see cref="GetPagedAsync"/></description></item>
///   <item><description>GetExpiredPortals → <see cref="GetExpiredAsync"/></description></item>
///   <item><description>CreatePortal → <see cref="AddAsync"/></description></item>
///   <item><description>UpdatePortalInfo → <see cref="UpdateAsync"/></description></item>
///   <item><description>DeletePortalInfo → <see cref="DeleteAsync"/></description></item>
/// </list>
/// </para>
/// </remarks>
public interface IPortalRepository
{
    /// <summary>
    /// Gets a portal by its unique identifier.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the <see cref="Portal"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET PortalController.GetPortal(ByVal PortalId As Integer).
    /// The original method retrieved from cache first, then database if not cached.
    /// </remarks>
    /// <example>
    /// <code>
    /// var portal = await portalRepository.GetByIdAsync(1);
    /// if (portal is not null)
    /// {
    ///     Console.WriteLine($"Portal Name: {portal.PortalName}");
    /// }
    /// </code>
    /// </example>
    Task<Portal?> GetByIdAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all portals in the system.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of all <see cref="Portal"/> entities.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET PortalController.GetPortals() which returned an ArrayList.
    /// The return type is now strongly-typed as IEnumerable&lt;Portal&gt;.
    /// </remarks>
    /// <example>
    /// <code>
    /// var portals = await portalRepository.GetAllAsync();
    /// foreach (var portal in portals)
    /// {
    ///     Console.WriteLine($"Portal: {portal.PortalName}");
    /// }
    /// </code>
    /// </example>
    Task<IEnumerable<Portal>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets portals whose name matches the specified filter expression.
    /// </summary>
    /// <param name="nameToMatch">
    /// The name or partial name to match. Supports wildcard matching with '%' character.
    /// Use "%" for all portals or include '%' for partial matches (e.g., "%test%").
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of matching <see cref="Portal"/> entities.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Partial implementation of VB.NET PortalController.GetPortalsByName.
    /// This version returns all matches without pagination. Use <see cref="GetPagedAsync"/>
    /// for paginated results.
    /// </remarks>
    /// <example>
    /// <code>
    /// var matchingPortals = await portalRepository.GetByNameAsync("%production%");
    /// </code>
    /// </example>
    Task<IEnumerable<Portal>> GetByNameAsync(string nameToMatch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of portals whose name matches the specified filter expression.
    /// </summary>
    /// <param name="nameToMatch">
    /// The name or partial name to match. Supports wildcard matching with '%' character.
    /// Use "%" for all portals or include '%' for partial matches (e.g., "%test%").
    /// </param>
    /// <param name="pageIndex">
    /// The zero-based index of the page to retrieve. Use -1 to retrieve all records.
    /// </param>
    /// <param name="pageSize">
    /// The maximum number of records to return per page. When pageIndex is -1,
    /// this parameter is ignored and all matching records are returned.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a tuple with the matching <see cref="Portal"/> entities and the total record count
    /// for pagination purposes.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET PortalController.GetPortalsByName(nameToMatch, pageIndex, pageSize, ByRef totalRecords).
    /// The original method used ByRef for totalRecords; this version returns a tuple.
    /// </remarks>
    /// <example>
    /// <code>
    /// var (portals, totalCount) = await portalRepository.GetPagedAsync("%test%", pageIndex: 0, pageSize: 10);
    /// Console.WriteLine($"Showing {portals.Count()} of {totalCount} total portals");
    /// </code>
    /// </example>
    Task<(IEnumerable<Portal> Portals, int TotalRecords)> GetPagedAsync(
        string nameToMatch,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all portals that have expired based on their <see cref="Portal.ExpiryDate"/>.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of expired <see cref="Portal"/> entities.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET PortalController.GetExpiredPortals() which called
    /// DataProvider.Instance().GetExpiredPortals(). Expired portals are those where
    /// ExpiryDate is not null and is less than the current date/time.
    /// </remarks>
    /// <example>
    /// <code>
    /// var expiredPortals = await portalRepository.GetExpiredAsync();
    /// foreach (var portal in expiredPortals)
    /// {
    ///     Console.WriteLine($"Portal {portal.PortalName} expired on {portal.ExpiryDate}");
    /// }
    /// </code>
    /// </example>
    Task<IEnumerable<Portal>> GetExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of portals in the system.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the total number of portals.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET DataProvider.Instance.GetPortalCount() which was used
    /// in PortalController.DeletePortal to verify this is not the last portal before deletion.
    /// </remarks>
    /// <example>
    /// <code>
    /// var count = await portalRepository.GetPortalCountAsync();
    /// if (count > 1)
    /// {
    ///     // Safe to delete a portal
    /// }
    /// </code>
    /// </example>
    Task<int> GetPortalCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a portal with the specified identifier exists.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal to check.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is
    /// <c>true</c> if a portal with the specified ID exists; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method is optimized for existence checking without loading the full entity.
    /// Use this instead of GetByIdAsync when you only need to verify existence.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (await portalRepository.ExistsAsync(portalId))
    /// {
    ///     // Portal exists, proceed with operation
    /// }
    /// </code>
    /// </example>
    Task<bool> ExistsAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new portal to the repository.
    /// </summary>
    /// <param name="portal">The portal entity to add.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database operations.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the added <see cref="Portal"/> entity with its generated <see cref="Portal.PortalId"/>
    /// and <see cref="Portal.GUID"/> populated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.CreatePortal which called
    /// DataProvider.Instance().CreatePortal() with multiple parameters.
    /// </para>
    /// <para>
    /// The original method had two overloads - a private one taking basic parameters
    /// and a public one with full wizard parameters. This interface method corresponds
    /// to the data access layer portion only.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="portal"/> is null.</exception>
    /// <example>
    /// <code>
    /// var newPortal = new Portal
    /// {
    ///     PortalName = "My New Portal",
    ///     Currency = "USD",
    ///     HomeDirectory = "Portals/1"
    /// };
    /// var createdPortal = await portalRepository.AddAsync(newPortal);
    /// Console.WriteLine($"Created portal with ID: {createdPortal.PortalId}");
    /// </code>
    /// </example>
    Task<Portal> AddAsync(Portal portal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing portal in the repository.
    /// </summary>
    /// <param name="portal">The portal entity with updated values.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database operations.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.UpdatePortalInfo which had two overloads:
    /// <list type="bullet">
    ///   <item><description>UpdatePortalInfo(ByVal Portal As PortalInfo)</description></item>
    ///   <item><description>UpdatePortalInfo(ByVal PortalId, ByVal PortalName, ... multiple params)</description></item>
    /// </list>
    /// This interface method uses the entity-based approach.
    /// </para>
    /// <para>
    /// The original method also cleared the portal cache after update via
    /// DataCache.ClearPortalCache(PortalId, True). Cache invalidation should be
    /// handled at the service layer in the new architecture.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="portal"/> is null.</exception>
    /// <example>
    /// <code>
    /// var portal = await portalRepository.GetByIdAsync(1);
    /// if (portal is not null)
    /// {
    ///     portal.PortalName = "Updated Portal Name";
    ///     await portalRepository.UpdateAsync(portal);
    /// }
    /// </code>
    /// </example>
    Task UpdateAsync(Portal portal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a portal by its unique identifier.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal to delete.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database operations.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.DeletePortalInfo which called
    /// DataProvider.Instance().DeletePortalInfo(PortalId) and cleared the host cache.
    /// </para>
    /// <para>
    /// Important: The original DeletePortal method in PortalController performed
    /// additional cleanup (files, folders, aliases) before calling DeletePortalInfo.
    /// This repository method only handles the database record deletion.
    /// File system and alias cleanup should be handled at the service layer.
    /// </para>
    /// <para>
    /// Before deleting, verify this is not the last portal using <see cref="GetPortalCountAsync"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var portalCount = await portalRepository.GetPortalCountAsync();
    /// if (portalCount > 1)
    /// {
    ///     await portalRepository.DeleteAsync(portalId);
    /// }
    /// </code>
    /// </example>
    Task DeleteAsync(int portalId, CancellationToken cancellationToken = default);
}
