// <copyright file="PagedResult.cs" company="DNN Migration Project">
// Copyright (c) DNN Migration Project. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// MIGRATION: This file replaces the legacy VB.NET pagination pattern where methods returned
// ArrayList with ByRef totalRecords output parameter. The new PagedResult<T> record
// encapsulates all pagination metadata in a strongly-typed, immutable response object.
// Legacy pattern: Function GetUsers(ByRef totalRecords As Integer) As ArrayList
// New pattern: Task<PagedResult<UserDto>> GetUsersAsync(int pageIndex, int pageSize)

namespace DnnMigration.Application.DTOs.Common;

/// <summary>
/// Generic pagination wrapper DTO that encapsulates paginated API responses.
/// </summary>
/// <typeparam name="T">The type of items in the paginated collection.</typeparam>
/// <remarks>
/// <para>
/// This foundational DTO is used by all list endpoints across Portal, Module, User, and Role APIs
/// to provide consistent paginated responses. It replaces the legacy VB.NET pattern of using
/// <c>ByRef totalRecords</c> output parameters with a unified, immutable response object.
/// </para>
/// <para>
/// MIGRATION: Converts legacy patterns:
/// <list type="bullet">
/// <item><description>VB.NET <c>ByRef totalRecords As Integer</c> → <see cref="TotalCount"/> property</description></item>
/// <item><description>VB.NET <c>ArrayList</c> return → strongly-typed <see cref="Items"/> collection</description></item>
/// <item><description>Separate pageIndex/pageSize/totalRecords parameters → unified <see cref="PagedResult{T}"/> response</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Creating a PagedResult using the factory method
/// var users = await userRepository.GetUsersAsync(pageIndex, pageSize);
/// var totalCount = await userRepository.GetTotalCountAsync();
/// var result = PagedResult&lt;UserDto&gt;.Create(users, pageIndex, pageSize, totalCount);
/// 
/// // Accessing pagination metadata
/// if (result.HasNextPage)
/// {
///     // Show next page button
/// }
/// </code>
/// </example>
public record PagedResult<T>
{
    /// <summary>
    /// Gets the collection of items for the current page.
    /// </summary>
    /// <value>
    /// An <see cref="IReadOnlyList{T}"/> containing the items for the current page.
    /// This collection is never null; an empty page returns an empty list.
    /// </value>
    /// <remarks>
    /// MIGRATION: Replaces legacy VB.NET <c>ArrayList</c> return type with a strongly-typed,
    /// read-only collection for improved type safety and immutability.
    /// </remarks>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Gets the zero-based index of the current page.
    /// </summary>
    /// <value>
    /// A non-negative integer representing the current page index.
    /// The first page has index 0.
    /// </value>
    /// <remarks>
    /// MIGRATION: Replaces legacy <c>pageIndex</c> parameter passed separately to data access methods.
    /// </remarks>
    public required int PageIndex { get; init; }

    /// <summary>
    /// Gets the maximum number of items per page.
    /// </summary>
    /// <value>
    /// A positive integer representing the page size.
    /// Must be greater than 0.
    /// </value>
    /// <remarks>
    /// MIGRATION: Replaces legacy <c>pageSize</c> parameter passed separately to data access methods.
    /// </remarks>
    public required int PageSize { get; init; }

    /// <summary>
    /// Gets the total number of items across all pages.
    /// </summary>
    /// <value>
    /// A non-negative integer representing the total count of items in the entire dataset.
    /// </value>
    /// <remarks>
    /// MIGRATION: Replaces legacy VB.NET <c>ByRef totalRecords As Integer</c> output parameter pattern
    /// where total count was passed by reference as a side effect of the query method.
    /// </remarks>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Gets the total number of pages based on the total count and page size.
    /// </summary>
    /// <value>
    /// A non-negative integer representing the total number of pages.
    /// Returns 0 if there are no items, or the ceiling of <see cref="TotalCount"/> divided by <see cref="PageSize"/>.
    /// </value>
    /// <remarks>
    /// This is a computed property that calculates the total pages using ceiling division
    /// to ensure partial pages are counted. For example, 25 items with page size 10 yields 3 pages.
    /// </remarks>
    public int TotalPages => PageSize > 0 
        ? (int)Math.Ceiling((double)TotalCount / PageSize) 
        : 0;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    /// <value>
    /// <c>true</c> if the current page index is greater than 0; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This computed property enables UI components to determine whether to show
    /// previous page navigation controls.
    /// </remarks>
    public bool HasPreviousPage => PageIndex > 0;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    /// <value>
    /// <c>true</c> if there are more pages after the current page; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This computed property enables UI components to determine whether to show
    /// next page navigation controls.
    /// </remarks>
    public bool HasNextPage => PageIndex + 1 < TotalPages;

    /// <summary>
    /// Creates a new <see cref="PagedResult{T}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="items">The collection of items for the current page. Cannot be null.</param>
    /// <param name="pageIndex">The zero-based index of the current page. Must be non-negative.</param>
    /// <param name="pageSize">The maximum number of items per page. Must be greater than 0.</param>
    /// <param name="totalCount">The total number of items across all pages. Must be non-negative.</param>
    /// <returns>A new <see cref="PagedResult{T}"/> instance containing the provided data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageIndex"/> is negative,
    /// <paramref name="pageSize"/> is less than or equal to 0,
    /// or <paramref name="totalCount"/> is negative.
    /// </exception>
    /// <example>
    /// <code>
    /// var items = new List&lt;UserDto&gt; { user1, user2, user3 };
    /// var result = PagedResult&lt;UserDto&gt;.Create(items, pageIndex: 0, pageSize: 10, totalCount: 25);
    /// 
    /// Console.WriteLine($"Page {result.PageIndex + 1} of {result.TotalPages}");
    /// Console.WriteLine($"Showing {result.Items.Count} of {result.TotalCount} total items");
    /// </code>
    /// </example>
    public static PagedResult<T> Create(
        IEnumerable<T> items,
        int pageIndex,
        int pageSize,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                "Page index must be non-negative.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                "Page size must be greater than zero.");
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalCount),
                totalCount,
                "Total count must be non-negative.");
        }

        return new PagedResult<T>
        {
            Items = items as IReadOnlyList<T> ?? items.ToList().AsReadOnly(),
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Creates an empty <see cref="PagedResult{T}"/> with the specified page parameters.
    /// </summary>
    /// <param name="pageIndex">The zero-based index of the current page. Must be non-negative.</param>
    /// <param name="pageSize">The maximum number of items per page. Must be greater than 0.</param>
    /// <returns>An empty <see cref="PagedResult{T}"/> instance with zero total count.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageIndex"/> is negative or <paramref name="pageSize"/> is less than or equal to 0.
    /// </exception>
    /// <remarks>
    /// This convenience method is useful for returning an empty result set without needing
    /// to create an empty collection explicitly.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Return empty result when no items match search criteria
    /// if (!users.Any())
    /// {
    ///     return PagedResult&lt;UserDto&gt;.Empty(pageIndex, pageSize);
    /// }
    /// </code>
    /// </example>
    public static PagedResult<T> Empty(int pageIndex, int pageSize)
    {
        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                "Page index must be non-negative.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                "Page size must be greater than zero.");
        }

        return new PagedResult<T>
        {
            Items = Array.Empty<T>(),
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = 0
        };
    }
}
