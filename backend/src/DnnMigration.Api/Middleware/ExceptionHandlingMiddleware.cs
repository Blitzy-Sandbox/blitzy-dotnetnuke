using System.Security;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

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
    /// The solution defines NO custom exception types; the application services throw concrete BCL /
    /// FluentValidation exceptions. For the known business cases (404/401/403/409) the exception message
    /// is a deliberate, user-facing business message and is intentionally exposed. Only the default
    /// (500) branch hides internals outside Development so that stack traces never leak in Production.
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

            // Business-rule conflicts, e.g. "Cannot delete the last remaining portal.",
            // "Cannot delete a tab that has child tabs.", "Cannot delete the portal administrator.".
            case InvalidOperationException:
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "conflict",
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = exception.Message
                };

            // Anything else is treated as an unexpected server fault. Internal detail (the full
            // exception, including stack trace) is exposed only in Development; Production receives a
            // generic, non-revealing message.
            default:
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "internal-server-error",
                    Title = "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = _environment.IsDevelopment()
                        ? exception.ToString()
                        : "An unexpected error occurred. Please contact support if the problem persists."
                };
        }
    }
}
