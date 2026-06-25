namespace DnnMigration.Application.DTOs.Common;

// MIGRATION: PagedResult<T> has no 1:1 legacy class. It generalizes the legacy paged-query pattern in
// Library/Components/Users/UserController.vb — e.g. GetUsers(portalId, pageIndex, pageSize, ByRef totalRecords)
// As ArrayList (and the GetUsersByEmail / GetUsersByUserName / GetUsersByProfileProperty overloads), each of
// which returned an ArrayList of items plus a ByRef totalRecords out-parameter. The returned ArrayList becomes
// Items, the ByRef totalRecords out-parameter becomes TotalCount, and pageIndex/pageSize become PageIndex/PageSize.
/// <summary>
/// Generic wrapper for one page of a list (GET-collection) endpoint payload: the page items plus
/// pagination metadata. T is bound by the consuming service to a DTO type
/// (for example PagedResult of PortalListItemDto) and must never be a Domain entity type.
/// This is a plain data shape; the API success envelope ({ data, meta }) is applied at the API layer.
/// </summary>
/// <typeparam name="T">The DTO item type contained in the page.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>The items in the current page. Never null; defaults to an empty list.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    // MIGRATION: replaces the legacy `ByRef totalRecords As Integer` out-parameter
    // (UserController.GetUsers and its overloads) — total matching records across all pages.
    /// <summary>Total number of matching records across all pages.</summary>
    public int TotalCount { get; init; }

    // MIGRATION: PageIndex is ZERO-BASED. Legacy DNN paging delegated to the ASP.NET 2.0
    // System.Web.Security.MembershipProvider.GetAllUsers(pageIndex, pageSize, out totalRecords),
    // whose pageIndex is 0-based; the 0-based convention is preserved here for behavioral parity.
    /// <summary>Zero-based index of the current page.</summary>
    public int PageIndex { get; init; }

    /// <summary>Number of items requested per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages, derived from TotalCount and PageSize.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    /// <summary>True when a previous (lower-index) page exists.</summary>
    public bool HasPreviousPage => PageIndex > 0;

    /// <summary>True when a subsequent (higher-index) page exists.</summary>
    public bool HasNextPage => PageIndex + 1 < TotalPages;
}
