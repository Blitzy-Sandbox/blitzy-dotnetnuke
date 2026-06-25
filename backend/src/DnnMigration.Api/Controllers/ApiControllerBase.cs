using DnnMigration.Application.DTOs.Common;
using DnnMigration.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

// MIGRATION: New supporting infrastructure (no legacy DotNetNuke analog). Provides the shared
// Result -> IActionResult translation for the thin BFF controllers, applying the standard success
// envelope ({ data, meta }) and the RFC 7807 ProblemDetails failure envelope. Centralizing this keeps
// the controllers genuinely thin (AAP 0.7.3: no business logic in controllers).
/// <summary>
/// Base class for the API's attribute-routed controllers. Exposes protected helpers that translate an
/// Application-layer Result / Result of T into an HTTP response using the project's standard success
/// envelope ({ data, meta }) and RFC 7807 ProblemDetails error envelope.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>Maps a value-producing result to 200 OK (enveloped) or 400 Bad Request.</summary>
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            return Failure(result, StatusCodes.Status400BadRequest);
        }

        return Ok(Envelope(result.Value));
    }

    /// <summary>Maps a single-read result to 200 OK (enveloped) or 404 Not Found.</summary>
    protected IActionResult HandleGet<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            return Failure(result, StatusCodes.Status404NotFound);
        }

        return Ok(Envelope(result.Value));
    }

    /// <summary>Maps a create result to 201 Created (with a Location header) or 400 Bad Request.</summary>
    protected IActionResult HandleCreated<T>(Result<T> result, string actionName, Func<T, object> routeValuesFactory)
    {
        if (result.IsFailure)
        {
            return Failure(result, StatusCodes.Status400BadRequest);
        }

        T value = result.Value;
        return CreatedAtAction(actionName, routeValuesFactory(value), Envelope(value));
    }

    /// <summary>Maps a delete result to 204 No Content or 400 Bad Request.</summary>
    protected IActionResult HandleDelete(Result result)
    {
        if (result.IsFailure)
        {
            return Failure(result, StatusCodes.Status400BadRequest);
        }

        return NoContent();
    }

    /// <summary>Maps an unpaged collection result to 200 OK with { data, meta: { count } } or 400.</summary>
    protected IActionResult HandleList<T>(Result<IEnumerable<T>> result)
    {
        if (result.IsFailure)
        {
            return Failure(result, StatusCodes.Status400BadRequest);
        }

        IReadOnlyCollection<T> items = result.Value as IReadOnlyCollection<T> ?? result.Value.ToList();
        return Ok(new { data = items, meta = new { count = items.Count } });
    }

    /// <summary>Maps a paged result to 200 OK with { data: items, meta: pagination } or 400.</summary>
    protected IActionResult HandlePaged<T>(Result<PagedResult<T>> result)
    {
        if (result.IsFailure)
        {
            return Failure(result, StatusCodes.Status400BadRequest);
        }

        PagedResult<T> page = result.Value;
        var meta = new
        {
            totalCount = page.TotalCount,
            pageIndex = page.PageIndex,
            pageSize = page.PageSize,
            totalPages = page.TotalPages,
            hasPreviousPage = page.HasPreviousPage,
            hasNextPage = page.HasNextPage
        };
        return Ok(new { data = page.Items, meta });
    }

    /// <summary>Clamps paging inputs: pageIndex is zero-based (minimum 0); pageSize is in [1, 100], default 20.</summary>
    protected static (int PageIndex, int PageSize) NormalizePaging(int pageIndex, int pageSize)
    {
        if (pageIndex < 0)
        {
            pageIndex = 0;
        }

        if (pageSize <= 0)
        {
            pageSize = DefaultPageSize;
        }
        else if (pageSize > MaxPageSize)
        {
            pageSize = MaxPageSize;
        }

        return (pageIndex, pageSize);
    }

    private static object Envelope<T>(T value) => new { data = value, meta = new { } };

    // MIGRATION: Operation-based Result -> HTTP status mapping. Domain.Common.Result has NO error-category
    // discriminator, so the caller chooses the failure status by operation (single-read -> 404, write -> 400).
    // A future ErrorType enum on Result is a // MIGRATION: candidate so NotFound/Validation/Conflict can be
    // distinguished centrally. Result.Errors is projected into the RFC 7807 ProblemDetails "errors" extension.
    private IActionResult Failure(Result result, int statusCode)
    {
        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status404NotFound ? "Resource Not Found" : "Request Could Not Be Processed",
            Type = $"https://httpstatuses.io/{statusCode}",
            Detail = result.Errors.Count > 0 ? string.Join("; ", result.Errors) : null
        };
        problemDetails.Extensions["errors"] = result.Errors;
        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }
}
