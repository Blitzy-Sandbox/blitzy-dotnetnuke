using System.Security;            // SecurityException
using System.Text.Json;           // JsonSerializer, JsonSerializerOptions, JsonSerializerDefaults, JsonNamingPolicy
using FluentValidation;           // ValidationException
using Microsoft.AspNetCore.Mvc;   // ProblemDetails, ValidationProblemDetails

namespace DnnMigration.Api.Middleware;

// MIGRATION: This middleware SUPERSEDES the legacy Web Forms universal error page
// Website/ErrorPage.aspx.vb (DotNetNuke.Services.Exceptions.ErrorPage), which is NOT ported as a page (AAP §0.6.4).
//   - Server.GetLastError             -> try/catch around the request pipeline (await _next(context))
//   - LogException(PageLoadException) -> structured ILogger<T> / Serilog logging with correlation id
//   - localized HTML error page        -> RFC 7807 Problem Details JSON (application/problem+json) (AAP §0.7.2)
//   - "status" query-string error page -> real HTTP status codes derived from the exception type

/// <summary>
/// Global ASP.NET Core middleware that catches every unhandled exception bubbling up from the downstream
/// request pipeline (controllers, model binding, application services, repositories) and converts it into a
/// standards-compliant RFC 7807 <c>application/problem+json</c> Problem Details response.
/// </summary>
/// <remarks>
/// <para>
/// This type is registered EARLY in the pipeline (right after request logging) via
/// <c>app.UseMiddleware&lt;ExceptionHandlingMiddleware&gt;()</c> in <c>Program.cs</c> so that it wraps the
/// entire downstream flow. The success-side counterpart to the error envelope produced here is
/// <c>DnnMigration.Application.Common.ApiResponse&lt;T&gt;</c> (the <c>{ data, meta }</c> envelope).
/// </para>
/// <para>
/// Concrete .NET exception types are mapped to deliberate HTTP status codes:
/// <see cref="ValidationException"/> -&gt; 400, <see cref="KeyNotFoundException"/> -&gt; 404,
/// <see cref="UnauthorizedAccessException"/> -&gt; 401, <see cref="SecurityException"/> -&gt; 403,
/// <see cref="InvalidOperationException"/> -&gt; 409, and anything else -&gt; 500. Internal exception detail is
/// only ever surfaced for the 500/default case when running in the Development environment, so production
/// responses never leak stack traces.
/// </para>
/// </remarks>
public sealed class ExceptionHandlingMiddleware
{
    /// <summary>The RFC 7807 media type emitted for every error response.</summary>
    private const string ProblemJsonContentType = "application/problem+json";

    /// <summary>Base URI used to compose the machine-readable <c>type</c> member of each Problem Details payload.</summary>
    private const string ErrorTypeBaseUri = "https://dnnmigration.com/errors/";

    /// <summary>
    /// Serializer options shared by every response. Uses Web defaults (camelCase property naming) for parity
    /// with the controllers' success envelope, and camelCases dictionary keys so that
    /// <see cref="ValidationProblemDetails.Errors"/> keys (for example <c>"PortalName"</c>) serialize as
    /// <c>"portalName"</c>.
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
    /// <param name="environment">Hosting environment, used to gate detailed error output to Development only.</param>
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
    /// Invokes the middleware: forwards the request downstream and converts any unhandled exception into an
    /// RFC 7807 Problem Details response.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    /// <summary>
    /// Logs the exception with correlation context and writes the corresponding Problem Details response.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="exception">The unhandled exception captured from the pipeline.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;

        // MIGRATION: replaces DNN LogException(...) with structured ILogger/Serilog logging incl. correlation id.
        _logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}. CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        // The response has already begun streaming to the client: headers/body are flushed and cannot be
        // rewritten into a Problem Details payload. Surface the failure to the host instead of corrupting it.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response already started for CorrelationId {CorrelationId}; cannot write Problem Details.",
                correlationId);
            throw exception;
        }

        var problemDetails = BuildProblemDetails(exception);

        // Surface the correlation id at the JSON root (ProblemDetails.Extensions is flattened via
        // [JsonExtensionData]) so clients and server logs can be correlated.
        problemDetails.Extensions["traceId"] = correlationId;

        context.Response.Clear();
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = ProblemJsonContentType;

        // CRITICAL: serialize against the RUNTIME type so the derived ValidationProblemDetails.Errors property
        // is emitted. Serializing against the static ProblemDetails type would drop it (System.Text.Json does
        // not perform polymorphic serialization by default).
        var payload = JsonSerializer.Serialize(problemDetails, problemDetails.GetType(), SerializerOptions);
        await context.Response.WriteAsync(payload);
    }

    /// <summary>
    /// Maps a concrete exception type to an RFC 7807 <see cref="ProblemDetails"/> (or
    /// <see cref="ValidationProblemDetails"/>) carrying the appropriate HTTP status, type URI, title and detail.
    /// </summary>
    /// <param name="exception">The unhandled exception to translate.</param>
    /// <returns>The Problem Details describing the failure.</returns>
    private ProblemDetails BuildProblemDetails(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validationException:
                // Group FluentValidation failures by the offending property so the client receives one entry
                // per field, each with all of its messages.
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

            case KeyNotFoundException:
                // Services throw with a deliberate, user-safe business message (e.g. "Portal {id} not found.").
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "not-found",
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = exception.Message
                };

            case UnauthorizedAccessException:
                // Raised by the auth flow for invalid credentials / invalid or expired refresh tokens.
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "unauthorized",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = exception.Message
                };

            case SecurityException:
                // Covers the AAP §0.6.2 Forbidden / unknown-permission-key case (no dedicated ForbiddenException type exists).
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "forbidden",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = exception.Message
                };

            case InvalidOperationException:
                // Business-rule conflicts, e.g. "Cannot delete the last remaining portal." /
                // "Cannot delete a tab that has child tabs." / "Cannot delete the portal administrator."
                return new ProblemDetails
                {
                    Type = ErrorTypeBaseUri + "conflict",
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = exception.Message
                };

            default:
                // Unknown/unexpected failure: never leak internals in Production; full detail only in Development.
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
