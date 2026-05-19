// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET PortalController.vb → C# 12 PortalRepository implementation
// Source: Library/Components/Portal/PortalController.vb
//
// Key transformations:
// 1) Replace SqlDataProvider with DnnDbContext dependency injection
// 2) Replace SqlHelper.ExecuteReader/ExecuteNonQuery calls with EF Core LINQ queries
// 3) Convert FillPortalInfo IDataReader hydration to EF Core entity tracking
// 4) Implement IPortalRepository interface with async/await pattern
// 5) Add CancellationToken support to all async methods
// 6) Use AsNoTracking() for read-only queries per EF Core 8 best practices
// 7) Use file-scoped namespace syntax
// 8) Use nullable reference types (Portal?)
// 9) Convert GetPortal stored procedure call to FirstOrDefaultAsync LINQ
// 10) Convert GetPortals stored procedure call to ToListAsync LINQ
// 11) Convert AddPortal stored procedure call to AddAsync + SaveChangesAsync
// 12) Convert UpdatePortalInfo stored procedure call to Update + SaveChangesAsync
// 13) Convert DeletePortal stored procedure call to Remove + SaveChangesAsync
// 14) Remove DataCache.ClearPortalCache calls (caching handled by application layer)
// 15) Preserve functional equivalence for all CRUD operations
//
// Original VB.NET methods replaced:
// - GetPortal(PortalId) → GetByIdAsync(portalId)
// - GetPortals() → GetAllAsync()
// - GetPortalsByName(nameToMatch, pageIndex, pageSize, totalRecords) → GetPagedAsync(...)
// - GetExpiredPortals() → GetExpiredAsync()
// - CreatePortal() → AddAsync(portal)
// - UpdatePortalInfo(Portal) → UpdateAsync(portal)
// - DeletePortalInfo(PortalId) → DeleteAsync(portalId)
//
// Original VB.NET patterns replaced:
// - DataProvider.Instance().GetPortal(PortalId) → _context.Portals.FirstOrDefaultAsync()
// - FillPortalInfo(dr) IDataReader hydration → EF Core automatic entity materialization
// - DataProvider.Instance().CreatePortal(...) → _context.Portals.AddAsync() + SaveChangesAsync()
// - DataProvider.Instance().UpdatePortalInfo(...) → _context.Update() + SaveChangesAsync()
// - DataProvider.Instance().DeletePortalInfo(PortalId) → _context.Remove() + SaveChangesAsync()
// - DataCache.ClearPortalCache(PortalId, True) → Removed (handled by application layer)
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core repository implementation for Portal entity CRUD operations.
/// </summary>
/// <remarks>
/// <para>
/// This repository implements <see cref="IPortalRepository"/> using EF Core 8 with
/// <see cref="DnnDbContext"/>. It provides data access operations for the Portal entity,
/// which is the core entity for DNN's multi-tenant architecture.
/// </para>
/// <para>
/// <strong>MIGRATION NOTE:</strong> This class replaces the legacy VB.NET
/// PortalController.vb data access patterns that used SqlHelper and stored procedures.
/// All SqlHelper.ExecuteReader/ExecuteNonQuery calls have been converted to EF Core
/// LINQ queries and change tracking operations.
/// </para>
/// <para>
/// <strong>Performance Considerations:</strong>
/// <list type="bullet">
/// <item><description>AsNoTracking() is used for read-only queries to avoid change tracking overhead</description></item>
/// <item><description>All methods support CancellationToken for cooperative cancellation</description></item>
/// <item><description>ConfigureAwait(false) is used throughout for library context</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Caching Note:</strong> The original PortalController cleared cache after mutations
/// via DataCache.ClearPortalCache(). In the new architecture, cache invalidation is handled
/// at the application/service layer, not in the repository.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // DI Registration in Program.cs:
/// builder.Services.AddScoped&lt;IPortalRepository, PortalRepository&gt;();
///
/// // Usage in a service:
/// public class PortalService : IPortalService
/// {
///     private readonly IPortalRepository _portalRepository;
///     
///     public async Task&lt;PortalDto?&gt; GetPortalAsync(int id)
///     {
///         var portal = await _portalRepository.GetByIdAsync(id);
///         return portal is null ? null : MapToDto(portal);
///     }
/// }
/// </code>
/// </example>
public class PortalRepository : IPortalRepository
{
    /// <summary>
    /// The EF Core DbContext for database operations.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Replaces the legacy SqlDataProvider/SqlHelper pattern.
    /// The DbContext is injected via constructor for dependency injection.
    /// </remarks>
    private readonly DnnDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalRepository"/> class.
    /// </summary>
    /// <param name="context">The EF Core DbContext for database operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    /// MIGRATION: Replaces the implicit dependency on DataProvider.Instance() and SqlHelper.
    /// The DbContext is now explicitly injected for testability and proper dependency management.
    /// </remarks>
    public PortalRepository(DnnDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.GetPortal(PortalId) which called
    /// DataProvider.Instance().GetPortal(PortalId) and used FillPortalInfo(dr) for IDataReader hydration.
    /// </para>
    /// <para>
    /// Original VB.NET pattern:
    /// <code>
    /// Dim dr As IDataReader = DataProvider.Instance().GetPortal(PortalId)
    /// Try
    ///     portal = FillPortalInfo(dr)
    /// Finally
    ///     If Not dr Is Nothing Then dr.Close()
    /// End Try
    /// </code>
    /// </para>
    /// <para>
    /// The original method also cached the result via DataCache.SetCache(). In the new
    /// architecture, caching is handled at the application/service layer.
    /// </para>
    /// </remarks>
    public async Task<Portal?> GetByIdAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace DataProvider.Instance().GetPortal(PortalId) stored procedure call
        // with EF Core LINQ query. AsNoTracking() used for read-only operation per EF Core 8 best practices.
        return await _context.Portals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortalId == portalId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.GetPortals() which called
    /// DataProvider.Instance().GetPortals() and used FillPortalInfoCollection(dr) for IDataReader hydration.
    /// </para>
    /// <para>
    /// Original VB.NET pattern:
    /// <code>
    /// Public Function GetPortals() As ArrayList
    ///     Return FillPortalInfoCollection(DataProvider.Instance().GetPortals())
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// The return type changed from ArrayList to IEnumerable&lt;Portal&gt; for type safety.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<Portal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace DataProvider.Instance().GetPortals() stored procedure call
        // with EF Core LINQ query. AsNoTracking() used for read-only operation.
        return await _context.Portals
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Partial implementation of VB.NET PortalController.GetPortalsByName without pagination.
    /// Uses EF.Functions.Like for SQL LIKE pattern matching to support wildcard searches.
    /// </para>
    /// <para>
    /// The nameToMatch parameter supports SQL LIKE wildcards (% for any characters).
    /// If nameToMatch is null or empty, all portals are returned.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<Portal>> GetByNameAsync(string nameToMatch, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace DataProvider.Instance().GetPortalsByName() stored procedure call
        // with EF Core LINQ query using EF.Functions.Like for SQL LIKE pattern matching.
        var query = _context.Portals.AsNoTracking();

        if (!string.IsNullOrEmpty(nameToMatch))
        {
            // Support SQL LIKE wildcards - if no wildcards provided, do exact match
            query = query.Where(p => EF.Functions.Like(p.PortalName, nameToMatch));
        }

        return await query
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.GetPortalsByName(nameToMatch, pageIndex, pageSize, ByRef totalRecords).
    /// </para>
    /// <para>
    /// Original VB.NET pattern:
    /// <code>
    /// Public Shared Function GetPortalsByName(ByVal nameToMatch As String, ByVal pageIndex As Integer, 
    ///     ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList
    ///     If pageIndex = -1 Then
    ///         pageIndex = 0
    ///         pageSize = Integer.MaxValue
    ///     End If
    ///     Return FillPortalInfoCollection(DataProvider.Instance().GetPortalsByName(nameToMatch, pageIndex, pageSize), totalRecords)
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// The ByRef totalRecords parameter is replaced with a tuple return type for cleaner API.
    /// </para>
    /// </remarks>
    public async Task<(IEnumerable<Portal> Portals, int TotalRecords)> GetPagedAsync(
        string nameToMatch,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Handle the original behavior where pageIndex = -1 means "get all"
        if (pageIndex == -1)
        {
            pageIndex = 0;
            pageSize = int.MaxValue;
        }

        var query = _context.Portals.AsNoTracking();

        // Apply name filter if provided
        if (!string.IsNullOrEmpty(nameToMatch))
        {
            query = query.Where(p => EF.Functions.Like(p.PortalName, nameToMatch));
        }

        // Get total count for pagination
        var totalRecords = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        // Apply pagination
        var portals = await query
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (portals, totalRecords);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.GetExpiredPortals() which called
    /// DataProvider.Instance().GetExpiredPortals().
    /// </para>
    /// <para>
    /// Original VB.NET pattern:
    /// <code>
    /// Public Shared Function GetExpiredPortals() As ArrayList
    ///     Return FillPortalInfoCollection(DataProvider.Instance().GetExpiredPortals())
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// Expired portals are those where ExpiryDate is not null and is less than the current UTC date/time.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<Portal>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace DataProvider.Instance().GetExpiredPortals() stored procedure call
        // with EF Core LINQ query. Expired portals have a non-null ExpiryDate in the past.
        return await _context.Portals
            .AsNoTracking()
            .Where(p => p.ExpiryDate != null && p.ExpiryDate < DateTime.UtcNow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET DataProvider.Instance.GetPortalCount() which was used
    /// in PortalController.DeletePortal to verify this is not the last portal before deletion.
    /// </para>
    /// <para>
    /// Original VB.NET usage:
    /// <code>
    /// Dim portalCount As Integer = DataProvider.Instance.GetPortalCount()
    /// If portalCount > 1 Then
    ///     ' Safe to delete
    /// End If
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<int> GetPortalCountAsync(CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace DataProvider.Instance().GetPortalCount() stored procedure call
        // with EF Core CountAsync() query.
        return await _context.Portals
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method is optimized for existence checking without loading the full entity.
    /// It generates an EXISTS SQL query rather than loading the entire portal record.
    /// </remarks>
    public async Task<bool> ExistsAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // Use AnyAsync for efficient existence check - generates EXISTS SQL query
        return await _context.Portals
            .AnyAsync(p => p.PortalId == portalId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.CreatePortal which called
    /// DataProvider.Instance().CreatePortal() with multiple parameters.
    /// </para>
    /// <para>
    /// Original VB.NET pattern:
    /// <code>
    /// PortalId = DataProvider.Instance().CreatePortal(PortalName, strCurrency, datExpiryDate, 
    ///     dblHostFee, dblHostSpace, intPageQuota, intUserQuota, intSiteLogHistory, HomeDirectory)
    /// </code>
    /// </para>
    /// <para>
    /// The original stored procedure returned the new PortalId. In EF Core, the PortalId
    /// is automatically populated on the entity after SaveChangesAsync when using identity columns.
    /// </para>
    /// <para>
    /// A GUID is generated for the portal if not already set, matching the original behavior
    /// where the stored procedure generated a NEWID() for the GUID column.
    /// </para>
    /// </remarks>
    public async Task<Portal> AddAsync(Portal portal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(portal);

        // MIGRATION: Generate GUID if not set (original stored procedure used NEWID())
        if (portal.GUID == Guid.Empty)
        {
            portal.GUID = Guid.NewGuid();
        }

        // MIGRATION: Replace DataProvider.Instance().CreatePortal() stored procedure call
        // with EF Core AddAsync + SaveChangesAsync pattern.
        await _context.Portals
            .AddAsync(portal, cancellationToken)
            .ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        // The PortalId is automatically populated by EF Core after SaveChangesAsync
        // when using identity columns (matching original stored procedure behavior)
        return portal;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.UpdatePortalInfo(Portal) which called
    /// DataProvider.Instance().UpdatePortalInfo() with all portal properties as parameters.
    /// </para>
    /// <para>
    /// Original VB.NET pattern:
    /// <code>
    /// DataProvider.Instance().UpdatePortalInfo(PortalId, PortalName, LogoFile, FooterText, ExpiryDate, 
    ///     UserRegistration, BannerAdvertising, Currency, AdministratorId, HostFee, HostSpace, 
    ///     PageQuota, UserQuota, PaymentProcessor, ProcessorUserId, ProcessorPassword, Description, 
    ///     KeyWords, BackgroundFile, SiteLogHistory, SplashTabId, HomeTabId, LoginTabId, UserTabId, 
    ///     DefaultLanguage, TimeZoneOffset, HomeDirectory)
    /// 
    /// ' clear portal settings
    /// DataCache.ClearPortalCache(PortalId, True)
    /// </code>
    /// </para>
    /// <para>
    /// The DataCache.ClearPortalCache() call is removed. Cache invalidation is handled
    /// at the application/service layer in the new architecture.
    /// </para>
    /// </remarks>
    public async Task UpdateAsync(Portal portal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(portal);

        // MIGRATION: Replace DataProvider.Instance().UpdatePortalInfo() stored procedure call
        // with EF Core Update + SaveChangesAsync pattern.
        // Note: If the entity is already being tracked, Update() marks it as Modified.
        // If not tracked, it attaches and marks as Modified.
        _context.Portals.Update(portal);

        await _context.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: DataCache.ClearPortalCache(PortalId, True) removed
        // Cache invalidation is handled at the application/service layer
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET PortalController.DeletePortalInfo(PortalId) which called
    /// DataProvider.Instance().DeletePortalInfo(PortalId).
    /// </para>
    /// <para>
    /// Original VB.NET pattern:
    /// <code>
    /// 'delete portal
    /// DataProvider.Instance().DeletePortalInfo(PortalId)
    /// 
    /// ' clear portal alias cache and entire portal
    /// DataCache.ClearHostCache(True)
    /// </code>
    /// </para>
    /// <para>
    /// Important: The original DeletePortal method in PortalController performed additional
    /// cleanup before calling DeletePortalInfo (skin assignments, portal users, files, folders).
    /// This repository method only handles the database record deletion. Additional cleanup
    /// should be handled at the service layer.
    /// </para>
    /// <para>
    /// The DataCache.ClearHostCache() call is removed. Cache invalidation is handled
    /// at the application/service layer in the new architecture.
    /// </para>
    /// </remarks>
    public async Task DeleteAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Replace DataProvider.Instance().DeletePortalInfo(PortalId) stored procedure call
        // with EF Core Remove + SaveChangesAsync pattern.

        // First, find the portal to delete
        var portal = await _context.Portals
            .FirstOrDefaultAsync(p => p.PortalId == portalId, cancellationToken)
            .ConfigureAwait(false);

        if (portal is not null)
        {
            _context.Portals.Remove(portal);

            await _context.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        // MIGRATION: DataCache.ClearHostCache(True) removed
        // Cache invalidation is handled at the application/service layer
    }
}
