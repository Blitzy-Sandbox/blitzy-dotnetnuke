// -----------------------------------------------------------------------------
//  ExceptionHandlingMiddleware.cs
//
//  MIGRATION: Replaces Library/HttpModules/Exception/ExceptionModule.vb - a legacy
//  DotNetNuke 4.x IHttpModule (VB.NET / ASP.NET Web Forms, .NET Framework 2.0) that
//  hooked HttpApplication.Error via AddHandler and logged unhandled request
//  exceptions to the DNN event log through
//  DotNetNuke.Services.Log.EventLog.ExceptionLogController.AddLog(...). The classic
//  HttpModule pipeline is rebuilt here as ASP.NET Core middleware (AAP sec. 0.4.1
//  "backend/.../Middleware/*.cs CREATE from Library/HttpModules/**"; sec. 0.6.3
//  "HTTP modules -> middleware").
//
//  Preserved semantics (from the legacy module):
//    * Centralized, single-point capture of unhandled request exceptions.
//    * Robust logging that never itself destabilizes the request pipeline.
//  Dropped (legacy Web-Forms artifacts with no equivalent in a stateless JSON API):
//    * IHttpModule / Init / AddHandler application.Error / Dispose / ModuleName.
//    * URL-extension filtering (.aspx / .asmx / .ashx) and the install.aspx /
//      installwizard.aspx skip - there are no Web Forms pages to guard.
//    * The DNN event-log write (ExceptionLogController) -> replaced by structured
//      ILogger<T> / Serilog logging.
//  New (AAP sec. 0.7.1 non-functional requirement):
//    * A consistent RFC 7807 Problem Details JSON response
//      (Content-Type: application/problem+json) carrying the per-request correlation
//      id. The legacy module only logged and let ASP.NET render an error page.
// -----------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Middleware;

/// <summary>
/// Convention-based ASP.NET Core middleware that provides the API's single,
/// centralized exception boundary: it wraps the remainder of the request pipeline in
/// a try/catch, logs any unhandled exception with the request's correlation id, and
/// emits a consistent RFC 7807 <c>application/problem+json</c> response.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally <em>convention-based</em> (constructor-style) middleware - a
/// plain class exposing a <see cref="RequestDelegate"/> constructor parameter and an
/// <c>InvokeAsync(HttpContext)</c> method - rather than an <c>IMiddleware</c>
/// implementation, because <c>Program.cs</c> wires it with the convention-based
/// <c>app.UseMiddleware&lt;ExceptionHandlingMiddleware&gt;()</c> overload. All three
/// constructor dependencies (<see cref="RequestDelegate"/>,
/// <see cref="ILogger{TCategoryName}"/> and <see cref="IHostEnvironment"/>) are DI
/// singletons, so injecting them in the constructor is safe for the single middleware
/// instance the pipeline creates.
/// </para>
/// <para>
/// Registration order matters: <c>Program.cs</c> registers this middleware SECOND,
/// immediately after the sibling <c>CorrelationIdMiddleware</c>. Because the
/// correlation-id middleware runs first, the id is already present on
/// <see cref="HttpContext.Items"/> (under
/// <see cref="CorrelationIdMiddleware.CorrelationIdItemKey"/>) by the time this
/// handler needs it; a fallback to <see cref="HttpContext.TraceIdentifier"/> keeps the
/// handler correct even if the ordering ever changes.
/// </para>
/// <para>
/// ASP.NET Core intentionally provides no <c>HttpResponse.Clear()</c>; once a response
/// has started (headers flushed) it cannot be rewritten. This middleware therefore
/// guards on <see cref="HttpResponse.HasStarted"/> and only writes the problem body
/// when the response has not yet begun.
/// </para>
/// </remarks>
// MIGRATION: Replaces Library/HttpModules/Exception/ExceptionModule.vb - a DotNetNuke IHttpModule that hooked
// HttpApplication.Error and logged unhandled request exceptions via ExceptionLogController.AddLog. The DNN
// event-log write is replaced by structured ILogger/Serilog logging, and (net-new per AAP sec. 0.7.1) an
// RFC 7807 application/problem+json response is emitted. The legacy .aspx/.asmx/.ashx URL filtering and
// install-page skip are dropped (no Web Forms pages in the stateless JSON API).
public sealed class ExceptionHandlingMiddleware
{
    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> used to serialize the
    /// <see cref="ProblemDetails"/> body. Configured for camelCase property names
    /// (Angular-friendly) and camelCase dictionary keys (so the nested
    /// <c>errors</c> field keys align with the camelCase DTO field names the client
    /// binds to), and to omit <c>null</c> values (so an absent <c>errors</c> or
    /// <c>detail</c> is not written). Held as a single static instance because
    /// <see cref="JsonSerializerOptions"/> is thread-safe once used and caching it
    /// avoids per-request allocation. This is deliberately independent of the MVC/DI
    /// JSON options, since middleware runs outside the MVC serialization pipeline.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next <see cref="RequestDelegate"/> in the request pipeline.</param>
    /// <param name="logger">The logger used to record unhandled exceptions (Serilog-backed).</param>
    /// <param name="environment">
    /// The host environment, used to gate leakage of exception detail: full exception
    /// text is only surfaced in the response when
    /// <see cref="Microsoft.Extensions.Hosting.HostEnvironmentEnvExtensions.IsDevelopment(IHostEnvironment)"/>
    /// is <see langword="true"/>.
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
    /// Invokes the next component in the pipeline, catching any unhandled exception and
    /// converting it into a logged, correlation-tagged RFC 7807 problem response.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Logs the supplied exception and, when the response has not already started,
    /// writes the mapped RFC 7807 <see cref="ProblemDetails"/> body.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <param name="exception">The unhandled exception captured by <see cref="InvokeAsync"/>.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items.TryGetValue(
                                CorrelationIdMiddleware.CorrelationIdItemKey, out var value)
                            && value is string s
            ? s
            : context.TraceIdentifier;

        var (statusCode, title, errors) = Map(exception);

        // MIGRATION: legacy ExceptionModule wrote to the DNN event log; here we log structured (Serilog) with
        // the correlation id. No sensitive data (no bodies/headers/credentials) is logged.
        _logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}. CorrelationId={CorrelationId}, Status={StatusCode}",
            context.Request.Method, context.Request.Path, correlationId, statusCode);

        // ASP.NET Core has no HttpResponse.Clear(); if the response already started we cannot rewrite it.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response already started; cannot emit ProblemDetails for CorrelationId={CorrelationId}",
                correlationId);
            return;
        }

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{statusCode}",
            Title = title,
            Status = statusCode,
            // MIGRATION: never leak the exception detail/stack in Production (AAP sec. 0.7.1).
            Detail = _environment.IsDevelopment() ? exception.ToString() : title,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions));
    }

    // MIGRATION: exception -> HTTP status mapping. Application services use a null/bool not-found convention
    // (controllers translate that to 404), so this is the safety net for genuinely unhandled exceptions.
    /// <summary>
    /// Maps an exception to its HTTP status code, human-readable title, and (for
    /// validation failures) a property-keyed dictionary of error messages.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>
    /// A tuple of the HTTP status code, the problem title, and an optional errors
    /// dictionary (populated only for <see cref="ValidationException"/>).
    /// </returns>
    private static (int StatusCode, string Title, IDictionary<string, string[]>? Errors) Map(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validationException:
                var errors = validationException.Errors
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
                return (StatusCodes.Status400BadRequest, "One or more validation errors occurred.", errors);
            case KeyNotFoundException:
                return (StatusCodes.Status404NotFound, "The requested resource was not found.", null);
            case UnauthorizedAccessException:
                return (StatusCodes.Status401Unauthorized, "Authentication is required or has failed.", null);
            // MIGRATION: a deliberate business-rule conflict (e.g. refusing to delete a user who is a portal
            // administrator - the legacy UserController.DeleteUser deleteAdmin gate [UserController.vb L200])
            // maps to 409 Conflict. ConflictException.Message is a caller-controlled, non-sensitive business
            // statement, so it is safe to surface as the RFC 7807 title/detail even in Production.
            case ConflictException conflictException:
                return (StatusCodes.Status409Conflict, conflictException.Message, null);
            default:
                return (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null);
        }
    }
}
