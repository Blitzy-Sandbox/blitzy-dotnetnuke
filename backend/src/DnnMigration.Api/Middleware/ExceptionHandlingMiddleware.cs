using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Middleware;

// MIGRATION: Replaces the legacy DotNetNuke Web Forms global error handling — the
// Library/HttpModules/Exception/ExceptionModule.vb IHttpModule (which hooked HttpApplication.Error and
// logged the unhandled exception to the DNN EventLog) and the Library/Components/Exceptions hierarchy
// (BasePortalException / Exceptions.ProcessPageLoadException / ErrorContainer). No legacy logic is ported:
// DNN URL filtering, EventLog persistence, custom-error redirect/transfer, and the [Serializable] exception
// subtypes are out of scope. This middleware is the OUTERMOST safety net for unexpected exceptions and for
// DnnMigration.Domain.Common.DomainExceptions that bubble up, translating them to a consistent RFC 7807
// ProblemDetails error envelope { type, title, status, detail, errors } so clients see one error shape.
/// <summary>
/// Catches unhandled exceptions from downstream middleware/endpoints and writes a standardized RFC 7807
/// <see cref="ProblemDetails"/> response. Registered SECOND in the pipeline (immediately after
/// <see cref="CorrelationIdMiddleware"/>, see Program.cs) so emitted problems already carry the correlation id.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

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

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId =
            context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var stored)
            && stored is string storedId
                ? storedId
                : context.TraceIdentifier;

        // The correlation id is already attached to this log entry via the Serilog LogContext scope set by
        // CorrelationIdMiddleware (Enrich.FromLogContext). Never log sensitive data (passwords/tokens/PII).
        _logger.LogError(exception,
            "Unhandled exception processing {Method} {Path} (correlation id {CorrelationId}).",
            context.Request.Method, context.Request.Path, correlationId);

        var problem = BuildProblemDetails(exception);
        problem.Extensions["errors"] = new Dictionary<string, string[]>();
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (context.Response.HasStarted)
        {
            // The response has already begun streaming; status/headers/body can no longer be rewritten.
            _logger.LogWarning(
                "The response had already started; ExceptionHandlingMiddleware could not emit a ProblemDetails body for correlation id {CorrelationId}.",
                correlationId);
            return;
        }

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var payload = JsonSerializer.Serialize(problem, SerializerOptions);
        await context.Response.WriteAsync(payload);
    }

    private ProblemDetails BuildProblemDetails(Exception exception)
    {
        switch (exception)
        {
            // MIGRATION: DomainException (DnnMigration.Domain.Common) models a business-rule/invariant
            // violation surfaced as an exception -> 400 Bad Request. Its Message is a safe, user-facing
            // business message, so it is returned as the problem detail.
            case DomainException domainException:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Type = "urn:dnnmigration:error:bad-request",
                    Detail = domainException.Message
                };

            // MIGRATION: optional convenience mapping for services that signal a missing resource by
            // throwing KeyNotFoundException -> 404 Not Found.
            case KeyNotFoundException:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Type = "urn:dnnmigration:error:not-found",
                    Detail = "The requested resource was not found."
                };

            // MIGRATION: optional mapping for authorization failures thrown from application code ->
            // 403 Forbidden. Authentication challenges are emitted earlier as 401 by the JWT bearer
            // middleware; a thrown UnauthorizedAccessException represents an authenticated-but-forbidden action.
            case UnauthorizedAccessException:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Type = "urn:dnnmigration:error:forbidden",
                    Detail = "You do not have permission to perform this action."
                };

            // MIGRATION: any other exception is unexpected -> 500 Internal Server Error. Mirrors the legacy
            // ErrorContainer.vb behavior of revealing exc.ToString() only to superusers: details are exposed
            // ONLY in the Development environment; production returns a generic message so no stack trace or
            // internal detail leaks (AAP 0.7.5/0.7.6).
            default:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Type = "urn:dnnmigration:error:internal",
                    Detail = _environment.IsDevelopment()
                        ? exception.ToString()
                        : "An unexpected error occurred while processing the request."
                };
        }
    }
}
