namespace DnnMigration.Domain.Common;

/// <summary>
/// Immutable carrier for a single bounded page of results together with the total number of rows
/// that match the (unpaged) query. Returned by the paged repository/service read methods so the API
/// layer can emit both the page payload and the pagination <c>meta</c> (<c>totalCount</c>/<c>totalPages</c>).
/// </summary>
/// <remarks>
/// MIGRATION (QA finding — R6 Issue 1, unbounded list endpoints): the legacy DotNetNuke list readers
/// (e.g. <c>PortalController.GetPortals</c>, <c>UserController.GetUsers</c>) materialized the ENTIRE
/// result set into an <c>ArrayList</c> with no server-side limit, and the modern list endpoints
/// inherited that shape (every <c>GET /api/{resource}</c> returned the full dataset). This type is the
/// building block of the bounded server-side pagination that closes that finding: the repository fetches
/// only the requested window (<c>Skip</c>/<c>Take</c>) while a separate <c>COUNT</c> supplies
/// <see cref="TotalCount"/>, so the client can page without the server ever streaming an unbounded body.
/// It lives in the dependency-free Domain layer so every ring (Application, Infrastructure, Api) can use
/// the one shared shape.
/// </remarks>
/// <typeparam name="T">The element type of the page (a domain entity for repositories, a DTO for services).</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>The elements on the requested page. Never <see langword="null"/> (empty when no rows match).</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// The total number of rows matching the query BEFORE the <c>Skip</c>/<c>Take</c> window was applied.
    /// This is what the API surfaces as <c>meta.totalCount</c> and from which <c>meta.totalPages</c> is derived.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Initializes a new <see cref="PagedResult{T}"/>.
    /// </summary>
    /// <param name="items">The page elements. A <see langword="null"/> value is coerced to an empty list.</param>
    /// <param name="totalCount">The total number of matching rows across all pages.</param>
    public PagedResult(IReadOnlyList<T> items, int totalCount)
    {
        Items = items ?? Array.Empty<T>();
        TotalCount = totalCount;
    }

    /// <summary>An empty page (no items, zero total) — useful when a scope resolves to no rows.</summary>
    public static PagedResult<T> Empty { get; } = new(Array.Empty<T>(), 0);
}
