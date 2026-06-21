using System.Data.Common;
using System.Security;
using System.Text.Json;
using DnnMigration.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Api.Middleware;

// MIGRATION: This middleware SUPERSEDES the legacy Web Forms universal error page
// Website/ErrorPage.aspx.vb (DotNetNuke.Services.Exceptions.ErrorPage), which is NOT ported as a page (AAP §0.6.4).
//   - Server.GetLastError             -> try/catch around the request pipeline (await _next(context))
//   - LogException(PageLoadException) -> structured ILogger<T> / Serilog logging with correlation id
//   - localized HTML error page        -> RFC 7807 Problem Details JSON (application/problem+json) (AAP §0.7.2)
//   - "status" query-string error page -> real HTTP status codes derived from the exception type

/// <summary>
/// Global middleware that converts unhandled exceptions bubbling up from the downstream pipeline
/// (controllers, model binding, application services, repositories) into RFC 7807
/// <see href="https://datatracker.ietf.org/doc/html/rfc7807">Problem Details</see> JSON responses
/// with <c>Content-Type: application/problem+json</c>.
/// </summary>
/// <remarks>
/// This is convention-based middleware: it is registered once via
/// <c>app.UseMiddleware&lt;ExceptionHandlingMiddleware&gt;()</c> in the API composition root
/// (registered early so it wraps the entire request flow). It owns the production of the error
/// envelope and is the error-side counterpart to the success-side <c>{ data, meta }</c> envelope
/// (<c>DnnMigration.Application.Common.ApiResponse&lt;T&gt;</c>). It does not depend on the
/// <c>Controllers/</c> folder and emits Problem Details through a manual <see cref="JsonSerializer"/>
/// (the project does not register <c>IProblemDetailsService</c>).
/// </remarks>
public sealed class ExceptionHandlingMiddleware
{
    /// <summary>The RFC 7807 media type emitted for every error response.</summary>
    private const string ProblemJsonContentType = "application/problem+json";

    /// <summary>
    /// Base URI for the Problem Details <c>type</c> member. Concatenated with a per-error slug
    /// (e.g. <c>validation</c>, <c>not-found</c>) to form a stable, dereferenceable error type URI.
    /// </summary>
    private const string ErrorTypeBaseUri = "https://dnnmigration.com/errors/";

    /// <summary>
    /// Serializer options used to write the Problem Details payload. Uses the Web defaults
    /// (camelCase property naming) for parity with the success-side envelope and the controllers;
    /// <see cref="JsonSerializerOptions.DictionaryKeyPolicy"/> camelCases the keys of the
    /// <c>errors</c> dictionary (e.g. <c>"PortalName"</c> -&gt; <c>"portalName"</c>).
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { DictionaryKeyPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next delegate in the request pipeline.</param>
    /// <param name="logger">Structured logger used to record unhandled exceptions with a correlation id.</param>
    /// <param name="environment">
    /// Hosting environment, used to decide whether internal error detail (stack traces) may be exposed.
    /// Detail is only ever surfaced in Development.
    /// </param>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Invokes the middleware: forwards the request to the rest of the pipeline and converts any
    /// unhandled exception into an RFC 7807 Problem Details response.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>A task that completes when the request (or the error response) has been written.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // MIGRATION: replaces DNN Server.GetLastError with a try/catch wrapping the whole downstream pipeline.
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    /// <summary>
    /// Logs the exception with a correlation id and writes the RFC 7807 Problem Details response.
    /// </summary>
    /// <param name="context">The current request context.</param>
    /// <param name="exception">The unhandled exception caught from the downstream pipeline.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // ASP.NET Core assigns a per-request trace identifier; reuse it as the correlation id so that
        // the structured log entry and the response payload (errors.traceId) reference the same value.
        var correlationId = context.TraceIdentifier;

        // MIGRATION: replaces DNN LogException(PageLoadException) with structured ILogger/Serilog logging
        // that includes the request method, path, and correlation id.
        _logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}. CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        // If the response has already begun streaming to the client, the status code and headers are
        // committed and the body is (partially) flushed; we cannot safely rewrite it as Problem Details.
        // Surface the failure to the host instead of corrupting the response. This guard MUST run before
        // any attempt to Clear()/write the response.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response already started for CorrelationId {CorrelationId}; cannot write Problem Details.",
                correlationId);
            throw exception;
        }

        var problemDetails = BuildProblemDetails(exception);

        // Surface the correlation id at the JSON root via the Problem Details extension members so that
        // clients can quote it back for support and it can be cross-referenced with server logs.
        problemDetails.Extensions["traceId"] = correlationId;

        context.Response.Clear();
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = ProblemJsonContentType;

        // CRITICAL: serialize against the RUNTIME type (problemDetails.GetType()), not the static
        // ProblemDetails type. System.Text.Json does not serialize derived members polymorphically when
        // given the base type, so passing the runtime type is what guarantees the derived
        // ValidationProblemDetails.Errors dictionary is emitted.
        var payload = JsonSerializer.Serialize(problemDetails, problemDetails.GetType(), SerializerOptions);
        await context.Response.WriteAsync(payload);
    }

    /// <summary>
    /// Maps a caught exception to its RFC 7807 <see cref="ProblemDetails"/> representation, deriving a
    /// real HTTP status code from the concrete exception type.
    /// </summary>
    /// <param name="exception">The unhandled exception to translate.</param>
    /// <returns>
    /// A <see cref="ProblemDetails"/> (or <see cref="ValidationProblemDetails"/> for validation failures)
    /// populated with <c>type</c>, <c>title</c>, <c>status</c>, and <c>detail</c>.
    /// </returns>
    /// <remarks>
    /// The application services throw concrete BCL / FluentValidation exceptions plus the one dedicated
    /// <see cref="BusinessConflictException"/> (the single custom exception type) for business-rule conflicts.
    /// For the known business cases (404/401/403/409) the exception message is a deliberate, user-facing
    /// business message and is intentionally exposed. The server-fault branches (the default 500 branch and
    /// the 503 data-access branch) hide internals outside Development so that stack traces and internal
    /// implementation detail never leak in Production (QA Finding F1-1).
    /// </remarks>
    private ProblemDetails BuildProblemDetails(Exception exception)
    {
        switch (exception)
        {
            // FluentValidation ValidateAndThrow(Async) in the Create/Update service methods.
            case ValidationException validationException:
                var errors = validationException.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).ToArray());
                return new ValidationProblemDetails(errors)
                {
                    Type = ErrorTypeBaseUri + "validation",
                    Title = "Validation Error",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "One or more validation errors occurred."
                };

            // Services throw e.g. new KeyNotFoundException($"Portal {id} not found.").
            case KeyNotFoundException:
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "not-found",
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = exception.Message
                };

            // AuthService throws for invalid credentials / invalid or expired refresh token.
            case UnauthorizedAccessException:
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "unauthorized",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = exception.Message
                };

            // Forbidden / unknown permission-key case (AAP §0.6.2); no ForbiddenException type exists.
            case SecurityException:
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "forbidden",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = exception.Message
                };

            // MIGRATION (QA Finding F1-1): business-rule conflicts are now signalled by the dedicated
            // BusinessConflictException (e.g. "Cannot delete the last remaining portal.", "Cannot delete a
            // tab that has child tabs.", "Cannot delete the portal administrator.", "Cannot remove this user
            // from the role."). Mapping ONLY this type to 409 — rather than the general-purpose
            // InvalidOperationException, which EF Core ALSO raises for database/transient infrastructure
            // failures — keeps a real database outage from being misclassified as a 4xx client error. The
            // message is a deliberate, user-facing business message and is intentionally exposed.
            case BusinessConflictException:
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "conflict",
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = exception.Message
                };

            // MIGRATION (QA Finding F1-1): database / data-access infrastructure failures are SERVER faults,
            // not client conflicts. A failed write (DbUpdateException), any ADO.NET provider failure
            // (DbException — Microsoft.Data.SqlClient.SqlException derives from it), and EF Core's
            // transient-failure wrapper (an InvalidOperationException whose inner-exception chain contains a
            // DbException — e.g. when the database server is unreachable) are all mapped to 503 Service
            // Unavailable, so that 5xx-based monitoring/alerting fires and clients apply server-error
            // retry/backoff. Internal detail is suppressed outside Development (CWE-209), mirroring the 500
            // branch.
            case DbUpdateException:
            case DbException:
                return BuildServerFaultProblemDetails(
                    exception,
                    StatusCodes.Status503ServiceUnavailable,
                    "service-unavailable",
                    "Service Unavailable",
                    "The service is temporarily unable to process the request. Please try again later.");

            case InvalidOperationException when ContainsDatabaseFailure(exception):
                return BuildServerFaultProblemDetails(
                    exception,
                    StatusCodes.Status503ServiceUnavailable,
                    "service-unavailable",
                    "Service Unavailable",
                    "The service is temporarily unable to process the request. Please try again later.");

            // Anything else is treated as an unexpected server fault. Internal detail (the full
            // exception, including stack trace) is exposed only in Development; Production receives a
            // generic, non-revealing message.
            default:
                return BuildServerFaultProblemDetails(
                    exception,
                    StatusCodes.Status500InternalServerError,
                    "internal-server-error",
                    "An unexpected error occurred.",
                    "An unexpected error occurred. Please contact support if the problem persists.");
        }
    }

    /// <summary>
    /// Builds a server-fault <see cref="ProblemDetails"/> (5xx) whose <c>detail</c> exposes the full
    /// exception only in the Development environment; in every other environment a generic, non-revealing
    /// message is returned so that internal implementation detail never leaks to clients (CWE-209). Both the
    /// default 500 branch and the 503 data-access branch route through here so the production-safety gate is
    /// applied uniformly.
    /// </summary>
    /// <param name="exception">The unhandled exception being translated.</param>
    /// <param name="statusCode">The 5xx status code to emit (e.g. 500 or 503).</param>
    /// <param name="typeSlug">The error type slug appended to <see cref="ErrorTypeBaseUri"/>.</param>
    /// <param name="title">The human-readable Problem Details title.</param>
    /// <param name="productionDetail">The generic, safe detail returned outside Development.</param>
    /// <returns>A populated <see cref="ProblemDetails"/> for the server fault.</returns>
    private ProblemDetails BuildServerFaultProblemDetails(
        Exception exception,
        int statusCode,
        string typeSlug,
        string title,
        string productionDetail)
    {
        return new ProblemDetails
        {
            Type = ErrorTypeBaseUri + typeSlug,
            Title = title,
            Status = statusCode,
            Detail = _environment.IsDevelopment()
                ? exception.ToString()
                : productionDetail
        };
    }

    /// <summary>
    /// Determines whether the inner-exception chain of <paramref name="exception"/> contains a database
    /// failure — a <see cref="DbException"/> (the ADO.NET provider base type, from which
    /// <c>Microsoft.Data.SqlClient.SqlException</c> derives) or an EF Core <see cref="DbUpdateException"/>.
    /// EF Core surfaces transient/connection failures as an <see cref="InvalidOperationException"/> that
    /// WRAPS such a provider exception, so the chain is walked rather than only the top-level type being
    /// inspected (the top-level <see cref="DbException"/>/<see cref="DbUpdateException"/> cases handle the
    /// unwrapped forms).
    /// </summary>
    /// <param name="exception">The exception whose inner-exception chain is examined.</param>
    /// <returns><c>true</c> when a <see cref="DbException"/> or <see cref="DbUpdateException"/> is found.</returns>
    private static bool ContainsDatabaseFailure(Exception exception)
    {
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is DbException or DbUpdateException)
            {
                return true;
            }
        }

        return false;
    }
}
