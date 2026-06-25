using Serilog.Context;

namespace DnnMigration.Api.Middleware;

// MIGRATION: Net-new ASP.NET Core middleware; no legacy code is ported. It generalizes the per-exception
// correlation GUID that the legacy DotNetNuke BasePortalException
// (Library/Components/Exceptions/BasePortalException.vb, private field m_ExceptionGUID) created for each
// thrown exception into a single per-REQUEST correlation id. That id flows through structured logging
// (Serilog LogContext) and is echoed to the caller; the legacy per-request portal/user/url context capture
// is replaced by this id plus Serilog enrichment rather than being stored on exception objects.
/// <summary>
/// Assigns a correlation id to every request, exposes it on the <see cref="HttpContext"/>, echoes it on the
/// response via the <c>X-Correlation-ID</c> header, and pushes it into the Serilog <see cref="LogContext"/> so
/// all downstream log entries are correlated. Registered FIRST in the pipeline (see Program.cs).
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>The request/response header used to read and echo the correlation id.</summary>
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    /// <summary>The <see cref="HttpContext.Items"/> key under which the correlation id is stored.</summary>
    public const string CorrelationIdItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        // Make the id available to downstream middleware (e.g. ExceptionHandlingMiddleware) and to the
        // framework trace identifier so one value correlates logs, the response header, and the RFC 7807 body.
        context.Items[CorrelationIdItemKey] = correlationId;
        context.TraceIdentifier = correlationId;

        // Echo the id on the response. Registered via OnStarting so the header is written just before the
        // response is sent, avoiding "headers already sent" / "response has already started" errors.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        _logger.LogDebug("Request {Method} {Path} assigned correlation id {CorrelationId}.",
            context.Request.Method, context.Request.Path, correlationId);

        // Push the id into the Serilog LogContext so every downstream log entry carries CorrelationId
        // (requires Enrich.FromLogContext(), configured in Program.cs / appsettings.json).
        using (LogContext.PushProperty(CorrelationIdItemKey, correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValues))
        {
            var incoming = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                return incoming;
            }
        }

        return Guid.NewGuid().ToString();
    }
}
