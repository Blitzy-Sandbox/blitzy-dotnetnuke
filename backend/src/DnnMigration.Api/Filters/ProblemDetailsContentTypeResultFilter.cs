using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DnnMigration.Api.Filters;

// MIGRATION (QA Finding "RFC 7807 media type" / AAP §0.7.2 — errors use RFC 7807 Problem Details):
// the global ExceptionHandlingMiddleware (thrown exceptions) and the UseStatusCodePages +
// AddProblemDetails pipeline (framework EMPTY-body 401/404/405/429) already emit
// application/problem+json. This filter closes the remaining gap: RFC 7807 bodies produced by MVC
// as an ObjectResult — the [ApiController] InvalidModelStateResponseFactory 400 (malformed JSON body,
// invalid-type query binding), the [ApiController] 415 Unsupported Media Type (converted to a
// ProblemDetails by MVC's ClientErrorResultFilter), and controller-authored ControllerBase.Problem(...)
// / ValidationProblem(...) results (e.g. TabsController/ModulesController "Missing filter" and
// "Identifier mismatch") — would otherwise be content-negotiated to application/json; charset=utf-8.
// Forcing the single application/problem+json media type makes EVERY error response carry the uniform
// RFC 7807 content type, so clients keying on application/problem+json parse them consistently.

/// <summary>
/// MVC result filter that guarantees every RFC 7807 <see cref="ProblemDetails"/> (and
/// <see cref="ValidationProblemDetails"/>) body produced as an <see cref="ObjectResult"/> is written
/// with the <c>application/problem+json</c> media type, overriding the default content negotiation that
/// would otherwise emit <c>application/json</c> for these framework- and controller-produced error bodies.
/// </summary>
/// <remarks>
/// Implemented as an <see cref="IAlwaysRunResultFilter"/> so it runs even when the MVC pipeline
/// short-circuits the action — which is exactly how the automatic model-state 400, the 415 Unsupported
/// Media Type, and other client-error results are produced. It is registered globally in the API
/// composition root (<c>Program.cs</c> via <c>AddControllers(o =&gt; o.Filters.Add&lt;...&gt;())</c>) and
/// runs after MVC's built-in <c>ClientErrorResultFilter</c> has mapped client-error status results to
/// <see cref="ProblemDetails"/>, so it observes the final <see cref="ObjectResult"/>. Because
/// <see cref="ObjectResult.ContentTypes"/> alone does not constrain content negotiation under a wildcard
/// <c>Accept: */*</c> (MVC then lets the JSON formatter emit its primary <c>application/json</c> type), the
/// filter additionally registers an <see cref="Microsoft.AspNetCore.Http.HttpResponse.OnStarting(System.Func{object,System.Threading.Tasks.Task},object)"/>
/// callback that overwrites the <c>Content-Type</c> header to <c>application/problem+json</c> at the last
/// moment before the headers flush — guaranteeing the RFC 7807 media type regardless of the request's
/// <c>Accept</c> header. Success envelopes (<c>{ data, meta }</c>) are plain <see cref="ObjectResult"/>s
/// whose value is NOT a <see cref="ProblemDetails"/>, so they are left untouched; the
/// <c>ExceptionHandlingMiddleware</c> writes its body directly to the response (not via an
/// <see cref="ObjectResult"/>) and is likewise unaffected.
/// </remarks>
public sealed class ProblemDetailsContentTypeResultFilter : IAlwaysRunResultFilter
{
    /// <summary>The single RFC 7807 media type emitted for every Problem Details body.</summary>
    private const string ProblemJsonContentType = "application/problem+json";

    /// <summary>The Problem Details extension member used to surface the request correlation id.</summary>
    private const string TraceIdExtensionKey = "traceId";

    /// <inheritdoc />
    public void OnResultExecuting(ResultExecutingContext context)
    {
        // Only act on RFC 7807 payloads: an ObjectResult whose value is a ProblemDetails (this also
        // covers the derived ValidationProblemDetails carrying the field-level "errors" map).
        if (context.Result is ObjectResult objectResult && objectResult.Value is ProblemDetails problemDetails)
        {
            // (1) Constrain MVC content negotiation to application/problem+json. This is honored when the
            //     client sends an explicit "Accept: application/problem+json" — the formatter then writes
            //     that exact media type. It is, however, NOT sufficient on its own (see (3)).
            objectResult.ContentTypes.Clear();
            objectResult.ContentTypes.Add(ProblemJsonContentType);

            // (2) Defensive parity with ExceptionHandlingMiddleware / the AddProblemDetails CustomizeProblemDetails
            // callback: surface the correlation id at the JSON root if MVC's ProblemDetailsFactory did not
            // already attach one (so a client can quote it back and cross-reference the server logs).
            if (!problemDetails.Extensions.ContainsKey(TraceIdExtensionKey))
            {
                problemDetails.Extensions[TraceIdExtensionKey] = context.HttpContext.TraceIdentifier;
            }

            // (3) DETERMINISTIC media-type pin. With the wildcard "Accept: */*" that REST clients (and curl)
            //     send by default, MVC content negotiation does NOT honor ObjectResult.ContentTypes as an
            //     exclusive constraint: */* lets the selected JSON output formatter emit its PRIMARY media
            //     type — application/json; charset=utf-8 — even though application/problem+json is the only
            //     listed content type. (Verified at runtime: identical request with an explicit
            //     "Accept: application/problem+json" yields problem+json, whereas "*/*" yields json.) Setting
            //     ContentTypes therefore cannot, by itself, guarantee the RFC 7807 media type for the common
            //     case. We pin it at the HTTP layer instead: Response.OnStarting runs immediately before the
            //     response headers are flushed — AFTER the output formatter has set Content-Type to
            //     application/json — so overwriting it there wins deterministically regardless of the request
            //     Accept header. The callback is registered ONLY for ProblemDetails results, so success
            //     envelopes ({ data, meta }, whose Value is not a ProblemDetails) are never affected.
            var response = context.HttpContext.Response;
            response.OnStarting(static state =>
            {
                var resp = (HttpResponse)state;
                var current = resp.ContentType;

                // Rewrite only when the body is being emitted as a JSON variant (the formatter's
                // application/json[; charset], or an already-problem+json[; charset] that we normalize to the
                // canonical no-charset form for uniformity with the middleware/status-code-pages responses).
                // A non-JSON Content-Type is left untouched as a safety guard.
                if (string.IsNullOrEmpty(current)
                    || current.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
                    || current.StartsWith(ProblemJsonContentType, StringComparison.OrdinalIgnoreCase))
                {
                    resp.ContentType = ProblemJsonContentType;
                }

                return Task.CompletedTask;
            }, response);
        }
    }

    /// <inheritdoc />
    public void OnResultExecuted(ResultExecutedContext context)
    {
        // No post-execution work: the media type is fixed before the result is written.
    }
}
