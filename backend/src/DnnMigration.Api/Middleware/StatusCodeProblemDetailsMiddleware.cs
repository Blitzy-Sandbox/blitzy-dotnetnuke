// -----------------------------------------------------------------------------
//  StatusCodeProblemDetailsMiddleware.cs
//
//  MIGRATION (QA finding R10 Issue 1 - empty-body error responses): net-new
//  observability/consistency component with no legacy VB source. The sibling
//  ExceptionHandlingMiddleware only converts THROWN exceptions into RFC 7807
//  ProblemDetails, and the [ApiController] convention only converts results that a
//  controller ACTION returns (NotFound()/ValidationProblem()/the horizontal-403
//  ForbiddenProblem, etc.). Neither covers the status codes the framework produces
//  BELOW the MVC layer - the JWT bearer 401 challenge, the authorization 403, the
//  routing 405 (method not allowed), and the rate-limiter 429 rejection - which all
//  returned with an EMPTY body. QA reproduced this: an unauthenticated GET, a
//  forbidden request, a wrong-verb request, and a rate-limited request each returned
//  the correct status code but no application/problem+json body, breaking the
//  documented uniform error contract (AAP sec. 0.7.1 "consistent RFC 7807 error
//  responses" + "request correlation IDs in all responses").
//
//  This middleware closes that gap the same way the framework's own
//  StatusCodePagesMiddleware does: it runs the rest of the pipeline, then - ONLY when
//  the response terminated as an error (>= 400) WITHOUT a body already having been
//  produced - writes a correlation-tagged RFC 7807 ProblemDetails body for that
//  status code. Any response that already carries a body (MVC 400 validation, 404
//  NotFound(), 409 ConflictProblem, the 500 written by ExceptionHandlingMiddleware,
//  and the horizontal-403 ForbiddenProblem) sets a Content-Type and is therefore left
//  untouched, so this NEVER double-writes an existing body.
//
//  Pipeline placement (Program.cs sec. 5.2): registered immediately AFTER
//  ExceptionHandlingMiddleware and BEFORE CORS/rate-limiter/authentication/
//  authorization/MVC. That makes it INNER to the exception handler (a thrown
//  exception still propagates up to ExceptionHandlingMiddleware - this component only
//  inspects a NORMALLY-returned response) and OUTER to the components that emit the
//  empty-body 401/403/405/429 (so their status codes bubble back up to it).
// -----------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Middleware;

/// <summary>
/// Convention-based ASP.NET Core middleware that emits a uniform RFC 7807
/// <see cref="ProblemDetails"/> (<c>application/problem+json</c>) body for framework-
/// produced error responses (notably 401/403/405/429) that would otherwise terminate
/// with an empty body, carrying the per-request correlation id. Responses that already
/// wrote a body are left untouched.
/// </summary>
/// <remarks>
/// The empty-body detection deliberately mirrors the framework's
/// <c>StatusCodePagesMiddleware</c> guard: act only when the response has NOT started,
/// the status code is an error (&gt;= 400), no <c>Content-Length</c> was set, and no
/// <c>Content-Type</c> was set. Those four conditions together identify a
/// status-code-only response (no body), which is exactly the case the QA finding
/// reported for the JWT challenge (401), the authorization failure (403), the routing
/// method-not-allowed (405), and the rate-limiter rejection (429).
/// </remarks>
public sealed class StatusCodeProblemDetailsMiddleware
{
    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> used to serialize the emitted
    /// <see cref="ProblemDetails"/>. Matched to <c>ExceptionHandlingMiddleware</c>
    /// (camelCase property names and dictionary keys, null values omitted) so the two
    /// error paths produce an identical wire shape. Held as a single static instance
    /// because <see cref="JsonSerializerOptions"/> is thread-safe once used and caching
    /// it avoids per-request allocation.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusCodeProblemDetailsMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next <see cref="RequestDelegate"/> in the request pipeline.</param>
    public StatusCodeProblemDetailsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the remainder of the pipeline and, when the response terminated as a
    /// body-less error, writes the correlation-tagged RFC 7807 problem body.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Empty-body error detection (mirrors StatusCodePagesMiddleware). If ANY of these hold, a body was
        // already produced (or it is not an error) - leave the response exactly as-is so we never overwrite
        // an MVC 400/404/409, the ExceptionHandlingMiddleware 500, or the horizontal-403 ForbiddenProblem.
        if (context.Response.HasStarted
            || context.Response.StatusCode < StatusCodes.Status400BadRequest
            || context.Response.ContentLength.HasValue
            || !string.IsNullOrEmpty(context.Response.ContentType))
        {
            return;
        }

        var statusCode = context.Response.StatusCode;
        var title = TitleFor(statusCode);

        // Read the id established by CorrelationIdMiddleware (which runs OUTER to this component), with the
        // framework TraceIdentifier as a defensive fallback - identical to ExceptionHandlingMiddleware.
        var correlationId = context.Items.TryGetValue(
                                CorrelationIdMiddleware.CorrelationIdItemKey, out var value)
                            && value is string s
            ? s
            : context.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{statusCode}",
            Title = title,
            Status = statusCode,
            // Detail is the same safe, occurrence-appropriate title (never any framework internals). This
            // path handles auth/authorization/method/rate-limit outcomes, none of which carry sensitive data.
            Detail = title,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        // Setting ContentType here (after the guard confirmed it was empty) both marks the response as
        // carrying a body and prevents any re-entrancy. The security-headers and correlation-id headers were
        // registered via Response.OnStarting by the OUTER middleware and fire on this flush, so this body
        // still carries X-Content-Type-Options/X-Frame-Options/Referrer-Policy/CSP and X-Correlation-ID.
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions));
    }

    /// <summary>
    /// Returns a stable, human-readable, non-sensitive title for the supplied error
    /// status code. The 401 title matches <c>ExceptionHandlingMiddleware</c>'s
    /// <see cref="UnauthorizedAccessException"/> mapping so the two paths are
    /// indistinguishable to a client.
    /// </summary>
    /// <param name="statusCode">The HTTP error status code (&gt;= 400).</param>
    /// <returns>The problem title for <paramref name="statusCode"/>.</returns>
    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => "Authentication is required or has failed.",
        StatusCodes.Status403Forbidden => "You do not have permission to access this resource.",
        StatusCodes.Status405MethodNotAllowed => "The requested HTTP method is not allowed for this resource.",
        StatusCodes.Status406NotAcceptable => "The requested representation is not available.",
        StatusCodes.Status415UnsupportedMediaType => "The request media type is not supported.",
        StatusCodes.Status429TooManyRequests => "Too many requests. Please retry later.",
        StatusCodes.Status404NotFound => "The requested resource was not found.",
        StatusCodes.Status400BadRequest => "The request was invalid.",
        >= StatusCodes.Status500InternalServerError => "An unexpected error occurred.",
        _ => "The request could not be completed."
    };
}
