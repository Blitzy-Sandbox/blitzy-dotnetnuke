using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Middleware;

// MIGRATION: (QA F10 Issue #1, error-envelope consistency) Centralizes writing the canonical RFC 7807
// problem+json envelope for the FRAMEWORK-generated error responses that previously bypassed the three
// in-assembly producers (the model-validation InvalidModelStateResponseFactory, ExceptionHandlingMiddleware,
// and ApiControllerBase.BuildProblemResult). Those bypassing paths were:
//   - 401 Unauthorized  (JWT bearer challenge: bare WWW-Authenticate header, EMPTY body)
//   - 403 Forbidden     (authorization policy denial: bare, EMPTY body)
//   - 404 Not Found     (unmatched route: bare, EMPTY body)
//   - 415 Unsupported Media Type / 405 / 406 (framework client errors: wrong content type or empty body)
// Each emitted a bare/empty or "application/json" body instead of the "application/problem+json" envelope the
// AAP (Section 0.1.2 / 0.7.5) mandates. This writer reuses the EXACT serializer options, content type, and
// correlationId/traceId extensions as ExceptionHandlingMiddleware so EVERY error across the API now shares one
// byte-consistent shape: { type, title, status, detail, errors, correlationId, traceId } as problem+json.
/// <summary>
/// Writes the standardized RFC 7807 <see cref="ProblemDetails"/> envelope for framework-generated error
/// responses (JWT challenges, authorization denials, unmatched routes, unsupported media types) that do not
/// flow through the MVC/exception producers. Reuses
/// <see cref="ExceptionHandlingMiddleware.ProblemJsonOptions"/> and
/// <see cref="ExceptionHandlingMiddleware.ProblemJsonContentType"/> for a single, consistent error contract.
/// </summary>
internal static class ProblemDetailsResponseWriter
{
    /// <summary>
    /// Writes a problem+json body for <paramref name="statusCode"/> with the given <paramref name="type"/>,
    /// <paramref name="title"/>, and <paramref name="detail"/>, attaching the empty <c>errors</c> map and the
    /// <c>correlationId</c>/<c>traceId</c> extensions. A no-op when the response has already started streaming
    /// (status/headers/body can no longer be rewritten), mirroring ExceptionHandlingMiddleware's guard.
    /// </summary>
    public static Task WriteAsync(HttpContext context, int statusCode, string type, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        // Resolve the correlation id identically to ExceptionHandlingMiddleware / the validation factory so
        // every error envelope carries the SAME id that CorrelationIdMiddleware wrote (falling back to the
        // framework trace identifier when the item is absent, e.g. a request that errored before that middleware).
        string correlationId =
            context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var stored)
            && stored is string storedId
                ? storedId
                : context.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail
        };

        // Match the exception path's extension shape exactly: an empty (camelCased) field-error map plus the
        // correlation/trace identifiers. ProblemJsonOptions ignores nulls, so an empty map still serializes.
        problem.Extensions["errors"] = new Dictionary<string, string[]>();
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = ExceptionHandlingMiddleware.ProblemJsonContentType;

        string payload = JsonSerializer.Serialize(problem, ExceptionHandlingMiddleware.ProblemJsonOptions);
        return context.Response.WriteAsync(payload);
    }
}
