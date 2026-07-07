// -----------------------------------------------------------------------------
//  CorrelationIdMiddleware.cs
//
//  MIGRATION: Net-new observability component with NO legacy VB source. The classic
//  DotNetNuke 4.x application (VB.NET / ASP.NET Web Forms) had no correlation-id
//  concept - none of the Library/HttpModules/** handlers (e.g. the referenced
//  Library/HttpModules/Exception/ExceptionModule.vb) established or propagated a
//  per-request trace id. This middleware is introduced for the .NET 8 migration to
//  satisfy AAP sec. 0.7.1 ("request correlation IDs in all responses") and the
//  logging NFR ("correlation-ID propagation"). Its ONLY lineage to the legacy code
//  is the HttpModule -> ASP.NET Core middleware pattern; behavior is entirely new.
//
//  For every request it: (1) resolves a correlation id from the incoming
//  X-Correlation-ID header or generates one; (2) stores it on HttpContext.Items and
//  HttpContext.TraceIdentifier so downstream components (notably the sibling
//  ExceptionHandlingMiddleware and framework logs / RFC 7807 ProblemDetails) can read
//  it; (3) pushes it into the Serilog LogContext so every log line emitted during the
//  request carries the CorrelationId property; and (4) echoes it back on the
//  X-Correlation-ID response header for EVERY response - success and error alike.
// -----------------------------------------------------------------------------

using Serilog.Context;

namespace DnnMigration.Api.Middleware;

/// <summary>
/// Convention-based ASP.NET Core middleware that establishes or propagates a
/// per-request correlation id, surfaces it to downstream components and the logging
/// pipeline, and echoes it on the <c>X-Correlation-ID</c> response header for every
/// response.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally <em>convention-based</em> (constructor-style) middleware -
/// a plain class with a <see cref="RequestDelegate"/> constructor parameter and an
/// <c>InvokeAsync(HttpContext)</c> method - rather than an <c>IMiddleware</c>
/// implementation, because <c>Program.cs</c> wires it with the convention-based
/// <c>app.UseMiddleware&lt;CorrelationIdMiddleware&gt;()</c> overload and registers no
/// middleware factory.
/// </para>
/// <para>
/// Registration order matters: <c>Program.cs</c> registers this middleware FIRST
/// (immediately after <c>UseSerilogRequestLogging()</c>) and BEFORE the
/// <c>ExceptionHandlingMiddleware</c>, so the correlation id is already present on
/// <see cref="HttpContext.Items"/> when the exception handler builds its RFC 7807
/// response body and so it enriches every log line for the request.
/// </para>
/// <para>
/// The <c>X-Correlation-ID</c> header is written via
/// <see cref="HttpResponse.OnStarting(System.Func{System.Threading.Tasks.Task})"/>,
/// which runs just before response headers are flushed. This guarantees the header
/// appears on ALL responses - including 4xx/5xx produced by downstream components and
/// framework-generated 404s - because the callback fires regardless of which
/// component ultimately writes the response body.
/// </para>
/// </remarks>
// MIGRATION: Net-new observability component (AAP sec. 0.7.1 "request correlation IDs
// in all responses"). No direct DotNetNuke IHttpModule equivalent existed in
// Library/HttpModules/**. Establishes or propagates a per-request correlation id,
// pushes it into the Serilog LogContext (so every log line for the request carries
// it), and echoes it on the X-Correlation-ID response header for ALL responses.
public sealed class CorrelationIdMiddleware
{
    /// <summary>
    /// The request/response header name used to read an inbound correlation id and to
    /// echo the resolved id back to the caller. Kebab-case with a capital <c>X</c>
    /// exactly as expected by callers and the response contract.
    /// </summary>
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    /// <summary>
    /// The <see cref="HttpContext.Items"/> key under which the resolved correlation id
    /// is stored. The sibling <c>ExceptionHandlingMiddleware</c> reads
    /// <c>HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey]</c> to embed
    /// the id in its RFC 7807 problem-details payload, so this value must remain
    /// stable.
    /// </summary>
    public const string CorrelationIdItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="next">
    /// The next <see cref="RequestDelegate"/> in the request pipeline, invoked once the
    /// correlation id has been resolved and made available to downstream components.
    /// </param>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Resolves the correlation id for the current request, exposes it to downstream
    /// components and the logging pipeline, schedules it to be echoed on the response
    /// header, and invokes the remainder of the pipeline.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> that completes when the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        // Surface the id to downstream middleware (via Items) and to framework logs /
        // ProblemDetails (via TraceIdentifier).
        context.Items[CorrelationIdItemKey] = correlationId;
        context.TraceIdentifier = correlationId;

        // Echo on EVERY response (success and error). OnStarting runs just before
        // headers are flushed, guaranteeing the header is present even when a
        // downstream component writes the body. Use the IHeaderDictionary indexer
        // (implicit string -> StringValues) which OVERWRITES; never .Add(...), which
        // throws on a duplicate key.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // MIGRATION: push into Serilog LogContext so all logs during the request carry
        // CorrelationId (Program.cs enables .Enrich.FromLogContext()). The using block
        // scopes the enriched property to exactly this request.
        using (LogContext.PushProperty(CorrelationIdItemKey, correlationId))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// Returns the inbound <c>X-Correlation-ID</c> header value when present and
    /// non-blank; otherwise generates a fresh 32-character, hyphen-less id via
    /// <see cref="System.Guid.NewGuid()"/> formatted with the <c>"N"</c> specifier.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>The resolved, non-null correlation id.</returns>
    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}
