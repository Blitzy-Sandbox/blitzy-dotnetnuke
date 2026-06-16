namespace DnnMigration.Application.Common;

// MIGRATION: PagedResult<T> is a NEW construct introduced by the migration's API pagination standard
// (AAP §0.7.2). The legacy *Controller.vb data methods returned an ArrayList plus a `ByRef totalRecords`
// out-parameter (e.g. UserController.GetUsers(portalId, pageIndex, pageSize, ByRef totalRecords) and
// PortalController.GetPortalsByName); there is NO 1:1 legacy equivalent. Application services map the
// Domain repository paged tuple (IEnumerable<TEntity> Items, int TotalCount) into PagedResult<XDto>.
// The Domain layer must NOT reference this Application-layer type.

/// <summary>
/// Represents a single page of results together with its pagination metadata. Returned by Application
/// service methods that expose paged reads (for example, the list endpoints behind GET /api/v1/{entity}).
/// </summary>
/// <typeparam name="T">The element type of the page (typically a read DTO such as <c>PortalDto</c>).</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>The items contained in the current page. Never <c>null</c>; defaults to an empty list.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>The total number of items across all pages (the legacy <c>ByRef totalRecords</c>).</summary>
    public int TotalCount { get; init; }

    /// <summary>The zero-based index of the current page.</summary>
    public int PageIndex { get; init; }

    /// <summary>The maximum number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>
    /// The total number of pages, computed from <see cref="TotalCount"/> and <see cref="PageSize"/>.
    /// Returns <c>0</c> when <see cref="PageSize"/> is non-positive (guards against divide-by-zero).
    /// </summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Initializes an empty page. Useful for object initializers and deserialization.</summary>
    public PagedResult()
    {
    }

    /// <summary>Initializes a page from its items and pagination values.</summary>
    /// <param name="items">The items on the current page.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <param name="pageIndex">The zero-based index of the current page.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageIndex, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageIndex = pageIndex;
        PageSize = pageSize;
    }
}
