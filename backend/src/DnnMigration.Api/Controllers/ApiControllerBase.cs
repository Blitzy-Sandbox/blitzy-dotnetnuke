using System.Security.Claims;
using System.Text.Json;
using DnnMigration.Api.Authorization;
using DnnMigration.Api.Middleware;
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

    // MIGRATION (CP2 review — resource-controller tenant-isolation findings): DNN scopes every entity by
    // PortalId, and the legacy admin pages ran inside a single portal context, so a portal Administrator could
    // never act on another portal while host SuperUsers administered all portals. The JWT carries the principal's
    // portal as the "portalId" claim and host status as "isSuperUser" (both issued by JwtService). The helpers
    // below reproduce that rule: a non-SuperUser may only act on the portal carried in its own token, and a
    // client-supplied portalId that does not match the token is rejected BEFORE any service/data call.

    /// <summary>
    /// True when the authenticated principal is a DNN host SuperUser (the "isSuperUser" JWT claim parses to
    /// <see langword="true"/>). Host SuperUsers operate across every portal and therefore bypass per-tenant
    /// portalId checks. The claim value is <c>bool.ToString()</c> ("True"/"False"), so it is parsed
    /// case-insensitively with <see cref="bool.TryParse(string, out bool)"/>.
    /// </summary>
    protected bool IsSuperUser() =>
        bool.TryParse(User.FindFirstValue(DnnClaims.IsSuperUser), out bool isSuperUser) && isSuperUser;

    /// <summary>
    /// Enforces multi-tenant isolation for a portal-scoped request. Returns <see langword="null"/> when access is
    /// permitted (the caller then proceeds to the service); otherwise returns a 403 ProblemDetails result that the
    /// action must return immediately. A host SuperUser is always permitted; any other principal must carry a
    /// "portalId" claim equal to <paramref name="requestedPortalId"/> — preventing a client from acting on a portal
    /// other than its own by supplying a different portalId in the query string or request body.
    /// </summary>
    /// <param name="requestedPortalId">The portal id supplied by the client (query string or request body).</param>
    /// <returns>
    /// <see langword="null"/> when the request is allowed to proceed; otherwise a 403 <see cref="ProblemDetails"/>.
    /// </returns>
    protected IActionResult? EnforceTenant(int requestedPortalId)
    {
        // MIGRATION: DNN host SuperUsers (UserInfo.IsSuperUser) administer all portals -> bypass the tenant check.
        if (IsSuperUser())
        {
            return null;
        }

        // A non-SuperUser MUST carry a parseable "portalId" claim that matches the requested tenant.
        if (int.TryParse(User.FindFirstValue(DnnClaims.PortalId), out int tokenPortalId) &&
            tokenPortalId == requestedPortalId)
        {
            return null;
        }

        return TenantForbidden(requestedPortalId);
    }

    /// <summary>
    /// Nullable overload for portal-scoped requests whose portal id may be absent — e.g. a DNN host-level page
    /// (tab) whose <c>PortalID</c> is NULL (the legacy Null.NullInteger sentinel). A null portal id denotes a
    /// host-level resource that only a host SuperUser may act on; a non-null portal id is enforced exactly as
    /// <see cref="EnforceTenant(int)"/>. Returns <see langword="null"/> when the request may proceed.
    /// </summary>
    /// <param name="requestedPortalId">The (possibly null) portal id supplied by the client request body.</param>
    protected IActionResult? EnforceTenant(int? requestedPortalId)
    {
        if (requestedPortalId.HasValue)
        {
            return EnforceTenant(requestedPortalId.Value);
        }

        // MIGRATION: a null portal id is a host-level (PortalID NULL) resource -> only host SuperUsers may act on it.
        return IsSuperUser() ? null : TenantForbidden(requestedPortalId);
    }

    /// <summary>
    /// Builds a 403 Forbidden RFC 7807 ProblemDetails for a cross-tenant (or unauthorized host-level) access
    /// attempt, using the same envelope shape as <see cref="Failure"/>. The principal is authenticated but not
    /// authorized for the requested scope, so 403 (not 401) is returned. The token's actual portal is never
    /// echoed back, to avoid tenant enumeration.
    /// </summary>
    private IActionResult TenantForbidden(int? requestedPortalId)
    {
        string detail = requestedPortalId.HasValue
            ? $"The authenticated principal is not authorized to access portal {requestedPortalId.Value}."
            : "The authenticated principal is not authorized to perform this host-level operation.";
        // MIGRATION: (QA-1 Issue #3) emit the SAME uniform RFC 7807 envelope as ExceptionHandlingMiddleware
        // (urn type scheme + correlationId/traceId + application/problem+json) instead of a divergent
        // https://httpstatuses.io/403 envelope with no correlation extensions.
        return BuildProblemResult(StatusCodes.Status403Forbidden, detail, errors: null);
    }

    private static object Envelope<T>(T value) => new { data = value, meta = new { } };

    // MIGRATION: Operation-based Result -> HTTP status mapping. Domain.Common.Result has NO error-category
    // discriminator, so the caller chooses the failure status by operation (single-read -> 404, write -> 400).
    // A future ErrorType enum on Result is a // MIGRATION: candidate so NotFound/Validation/Conflict can be
    // distinguished centrally. Result.Errors is projected into the RFC 7807 ProblemDetails "errors" extension.
    private IActionResult Failure(Result result, int statusCode)
    {
        string? detail = result.Errors.Count > 0 ? string.Join("; ", result.Errors) : null;
        // MIGRATION: (QA-1 Issue #3) emit the SAME uniform RFC 7807 envelope as ExceptionHandlingMiddleware.
        // The flat business-error list (Result.Errors) is preserved as the "errors" extension; validation
        // errors are field-keyed, whereas business Result errors are field-less, so a flat array is faithful.
        return BuildProblemResult(statusCode, detail, result.Errors);
    }

    // MIGRATION: (QA-1 Issue #3, error-envelope consistency) Single builder for the project's RFC 7807 error
    // envelope, mirroring ExceptionHandlingMiddleware so EVERY error response (unhandled exception, model
    // validation, and Result/tenant failure) shares one shape: the "urn:dnnmigration:error:*" type scheme, a
    // status-aligned title, an "errors" extension, and correlationId/traceId resolved from the SAME
    // HttpContext.Items["CorrelationId"] key CorrelationIdMiddleware writes (falling back to TraceIdentifier),
    // served as "application/problem+json". RFC 7807 requires one title per type URI, so the titles match the
    // middleware ("Bad Request" / "Not Found" / "Forbidden").
    private IActionResult BuildProblemResult(int statusCode, string? detail, IReadOnlyCollection<string>? errors)
    {
        (string title, string type) = statusCode switch
        {
            StatusCodes.Status400BadRequest => ("Bad Request", "urn:dnnmigration:error:bad-request"),
            StatusCodes.Status404NotFound => ("Not Found", "urn:dnnmigration:error:not-found"),
            StatusCodes.Status403Forbidden => ("Forbidden", "urn:dnnmigration:error:forbidden"),
            _ => ("Internal Server Error", "urn:dnnmigration:error:internal")
        };

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail
        };

        // Resolve the correlation id identically to ExceptionHandlingMiddleware / the validation factory so a
        // single id correlates the response body, the X-Correlation-ID header, and the Serilog log scope.
        string correlationId =
            HttpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var stored)
            && stored is string storedId
                ? storedId
                : HttpContext.TraceIdentifier;

        // "errors" is always present (matching the middleware): the business-error array when supplied,
        // otherwise an empty object for failures that carry no field/category errors (e.g. tenant-forbidden).
        problemDetails.Extensions["errors"] = errors is not null
            ? (object)errors
            : new Dictionary<string, string[]>();
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = HttpContext.TraceIdentifier;

        // Return a ContentResult with an explicit ContentType (not an ObjectResult) so the response is ALWAYS
        // "application/problem+json"; MVC content negotiation would otherwise let the JSON output formatter emit
        // its default "application/json". Serializing with the shared ExceptionHandlingMiddleware options
        // (camelCase + ignore-null) makes this envelope byte-identical to the exception and validation paths.
        return new ContentResult
        {
            StatusCode = statusCode,
            ContentType = ExceptionHandlingMiddleware.ProblemJsonContentType,
            Content = JsonSerializer.Serialize(problemDetails, ExceptionHandlingMiddleware.ProblemJsonOptions)
        };
    }
}
