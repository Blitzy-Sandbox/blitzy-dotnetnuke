namespace DnnMigration.Application.Common;

// MIGRATION: ApiResponse<T> and the { data, meta } success envelope are NEW constructs introduced by the
// migration's API response standard (AAP §0.7.2). Legacy *Controller.vb returned typed *Info objects /
// ArrayList / DataSet directly with NO envelope; there is no 1:1 legacy equivalent. This is the success-side
// counterpart to the RFC 7807 Problem Details emitted for errors by the Api's ExceptionHandlingMiddleware.
// Defined in the Application layer so the Application and Api layers share one response contract.

/// <summary>
/// The uniform success-response envelope: <c>{ data, meta }</c>.
/// </summary>
/// <typeparam name="T">The type of the payload carried in <see cref="Data"/>.</typeparam>
public sealed class ApiResponse<T>
{
    /// <summary>The response payload.</summary>
    public T? Data { get; init; }

    /// <summary>Optional response metadata (pagination and/or correlation information). May be <c>null</c>.</summary>
    public ApiResponseMeta? Meta { get; init; }

    /// <summary>Initializes an empty envelope. Useful for object initializers and deserialization.</summary>
    public ApiResponse()
    {
    }

    /// <summary>Initializes the envelope with a payload and optional metadata.</summary>
    /// <param name="data">The response payload.</param>
    /// <param name="meta">Optional response metadata.</param>
    public ApiResponse(T data, ApiResponseMeta? meta = null)
    {
        Data = data;
        Meta = meta;
    }
}

/// <summary>
/// Inference-friendly factory for <see cref="ApiResponse{T}"/>. Typically used at the Api/controller
/// boundary, for example <c>return Ok(ApiResponse.Success(dto));</c>.
/// </summary>
public static class ApiResponse
{
    /// <summary>Creates a success envelope wrapping <paramref name="data"/> with optional <paramref name="meta"/>.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    public static ApiResponse<T> Success<T>(T data, ApiResponseMeta? meta = null) => new(data, meta);
}

/// <summary>
/// Metadata accompanying a success response: pagination details (for list/paged endpoints) and/or a
/// correlation identifier (for log/trace correlation). All members are optional.
/// </summary>
public sealed class ApiResponseMeta
{
    /// <summary>The zero-based index of the current page, when the response is paged.</summary>
    public int? PageIndex { get; init; }

    /// <summary>The page size, when the response is paged.</summary>
    public int? PageSize { get; init; }

    /// <summary>The total number of items across all pages, when the response is paged.</summary>
    public int? TotalCount { get; init; }

    /// <summary>The total number of pages, when the response is paged.</summary>
    public int? TotalPages { get; init; }

    /// <summary>The correlation identifier for this response, for log/trace correlation.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Builds pagination metadata from a <see cref="PagedResult{T}"/>, optionally including a correlation id.
    /// </summary>
    /// <typeparam name="T">The page element type.</typeparam>
    /// <param name="page">The paged result whose pagination values are copied.</param>
    /// <param name="correlationId">An optional correlation identifier.</param>
    public static ApiResponseMeta FromPage<T>(PagedResult<T> page, string? correlationId = null) => new()
    {
        PageIndex = page.PageIndex,
        PageSize = page.PageSize,
        TotalCount = page.TotalCount,
        TotalPages = page.TotalPages,
        CorrelationId = correlationId
    };
}
