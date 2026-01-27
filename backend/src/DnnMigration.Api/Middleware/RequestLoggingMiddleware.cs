// -----------------------------------------------------------------------------
// RequestLoggingMiddleware.cs
// ASP.NET Core middleware for Serilog-based request/response logging.
// Captures HTTP request details (method, path, query string, content type, 
// content length), response details (status code, elapsed time), and logs 
// them using Serilog structured logging.
// -----------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;

namespace DnnMigration.Api.Middleware;

/// <summary>
/// ASP.NET Core middleware for request/response logging using Serilog.
/// Provides observability across all API endpoints by logging request start,
/// completion with duration, and any errors that occur during processing.
/// </summary>
/// <remarks>
/// This middleware captures:
/// - Request details: HTTP method, path, query string, content type, content length
/// - Response details: Status code, elapsed time in milliseconds
/// - Correlation/Request ID from HttpContext.TraceIdentifier
/// 
/// Log levels are determined by response status code:
/// - 2xx, 3xx: Information level
/// - 4xx: Warning level
/// - 5xx: Error level
/// </remarks>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate in the request pipeline.</param>
    /// <param name="logger">The logger instance for recording request/response details.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="next"/> or <paramref name="logger"/> is null.
    /// </exception>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the HTTP request and logs request/response details.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method:
    /// 1. Captures request details before invoking the next middleware
    /// 2. Measures elapsed time using a high-resolution Stopwatch
    /// 3. Invokes the next middleware in the pipeline
    /// 4. Logs the completed request with response details and timing
    /// 5. Handles any logging exceptions gracefully to prevent pipeline disruption
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Extract request details early to ensure they're captured even if the request fails
        var requestMethod = context.Request.Method;
        var requestPath = context.Request.Path.Value ?? string.Empty;
        var queryString = context.Request.QueryString.HasValue 
            ? context.Request.QueryString.Value 
            : string.Empty;
        var contentType = context.Request.ContentType ?? string.Empty;
        var contentLength = context.Request.ContentLength;
        var correlationId = GetCorrelationId(context);

        // Start timing the request
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Push correlation ID to Serilog context for all downstream logging
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("HttpMethod", requestMethod))
            using (LogContext.PushProperty("RequestPath", requestPath))
            {
                // Log request start at debug level (verbose, won't appear in production by default)
                LogRequestStart(requestMethod, requestPath, queryString, contentType, contentLength, correlationId);

                // Invoke the next middleware in the pipeline
                await _next(context).ConfigureAwait(false);

                // Stop the timer
                stopwatch.Stop();

                // Log request completion with response details
                LogRequestCompletion(
                    requestMethod,
                    requestPath,
                    queryString,
                    contentType,
                    contentLength,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
        }
        catch (Exception ex) when (LogRequestException(ex, requestMethod, requestPath, queryString, correlationId, stopwatch.ElapsedMilliseconds))
        {
            // This catch block with filter is used for logging only.
            // The exception is always re-thrown because the filter returns false.
            // This pattern ensures logging happens even if an exception occurs.
            throw;
        }
    }

    /// <summary>
    /// Gets the correlation ID from the request headers or falls back to TraceIdentifier.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The correlation ID for request tracking.</returns>
    private static string GetCorrelationId(HttpContext context)
    {
        // Check for common correlation ID headers
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationHeader) 
            && !string.IsNullOrWhiteSpace(correlationHeader))
        {
            return correlationHeader.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Request-ID", out var requestIdHeader) 
            && !string.IsNullOrWhiteSpace(requestIdHeader))
        {
            return requestIdHeader.ToString();
        }

        // Fall back to ASP.NET Core's built-in TraceIdentifier
        return context.TraceIdentifier;
    }

    /// <summary>
    /// Logs the start of a request at debug level.
    /// </summary>
    private void LogRequestStart(
        string method,
        string path,
        string? queryString,
        string? contentType,
        long? contentLength,
        string correlationId)
    {
        try
        {
            _logger.LogDebug(
                "HTTP {HttpMethod} {RequestPath} starting - QueryString: {QueryString}, ContentType: {ContentType}, ContentLength: {ContentLength}, CorrelationId: {CorrelationId}",
                method,
                path,
                queryString ?? string.Empty,
                contentType ?? string.Empty,
                contentLength,
                correlationId);
        }
        catch (Exception ex)
        {
            // Handle logging failure gracefully - don't break the request pipeline
            HandleLoggingFailure(ex, "LogRequestStart");
        }
    }

    /// <summary>
    /// Logs the completion of a request with appropriate log level based on status code.
    /// </summary>
    private void LogRequestCompletion(
        string method,
        string path,
        string? queryString,
        string? contentType,
        long? contentLength,
        int statusCode,
        long elapsedMilliseconds,
        string correlationId)
    {
        try
        {
            var logLevel = DetermineLogLevel(statusCode);

            // Use structured logging with Serilog
            var contextLogger = Log.ForContext("HttpMethod", method)
                .ForContext("RequestPath", path)
                .ForContext("QueryString", queryString ?? string.Empty)
                .ForContext("ContentType", contentType ?? string.Empty)
                .ForContext("ContentLength", contentLength)
                .ForContext("StatusCode", statusCode)
                .ForContext("ElapsedMilliseconds", elapsedMilliseconds)
                .ForContext("CorrelationId", correlationId);

            var message = "HTTP {HttpMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds}ms";

            switch (logLevel)
            {
                case LogLevel.Information:
                    contextLogger.Information(
                        message,
                        method,
                        path,
                        statusCode,
                        elapsedMilliseconds);
                    break;

                case LogLevel.Warning:
                    contextLogger.Warning(
                        message + " - Client error",
                        method,
                        path,
                        statusCode,
                        elapsedMilliseconds);
                    break;

                case LogLevel.Error:
                    contextLogger.Error(
                        message + " - Server error",
                        method,
                        path,
                        statusCode,
                        elapsedMilliseconds);
                    break;

                default:
                    contextLogger.Information(
                        message,
                        method,
                        path,
                        statusCode,
                        elapsedMilliseconds);
                    break;
            }

            // Also log via ILogger for integration with other logging providers
            switch (logLevel)
            {
                case LogLevel.Information:
                    _logger.LogInformation(
                        "HTTP {HttpMethod} {RequestPath}{QueryString} responded {StatusCode} in {ElapsedMilliseconds}ms [CorrelationId: {CorrelationId}]",
                        method,
                        path,
                        queryString ?? string.Empty,
                        statusCode,
                        elapsedMilliseconds,
                        correlationId);
                    break;

                case LogLevel.Warning:
                    _logger.LogWarning(
                        "HTTP {HttpMethod} {RequestPath}{QueryString} responded {StatusCode} in {ElapsedMilliseconds}ms - Client error [CorrelationId: {CorrelationId}]",
                        method,
                        path,
                        queryString ?? string.Empty,
                        statusCode,
                        elapsedMilliseconds,
                        correlationId);
                    break;

                case LogLevel.Error:
                    _logger.LogError(
                        "HTTP {HttpMethod} {RequestPath}{QueryString} responded {StatusCode} in {ElapsedMilliseconds}ms - Server error [CorrelationId: {CorrelationId}]",
                        method,
                        path,
                        queryString ?? string.Empty,
                        statusCode,
                        elapsedMilliseconds,
                        correlationId);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Handle logging failure gracefully - don't break the request pipeline
            HandleLoggingFailure(ex, "LogRequestCompletion");
        }
    }

    /// <summary>
    /// Logs an exception that occurred during request processing.
    /// This method is used as an exception filter and always returns false to re-throw.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="path">The path of the request.</param>
    /// <param name="queryString">The query string of the request.</param>
    /// <param name="correlationId">The correlation ID for the request.</param>
    /// <param name="elapsedMilliseconds">The elapsed time in milliseconds.</param>
    /// <returns>Always returns false to allow exception propagation.</returns>
    private bool LogRequestException(
        Exception exception,
        string method,
        string path,
        string? queryString,
        string correlationId,
        long elapsedMilliseconds)
    {
        try
        {
            var contextLogger = Log.ForContext("HttpMethod", method)
                .ForContext("RequestPath", path)
                .ForContext("QueryString", queryString ?? string.Empty)
                .ForContext("ElapsedMilliseconds", elapsedMilliseconds)
                .ForContext("CorrelationId", correlationId)
                .ForContext("ExceptionType", exception.GetType().FullName)
                .ForContext("ExceptionMessage", exception.Message);

            contextLogger.Error(
                exception,
                "HTTP {HttpMethod} {RequestPath} threw an exception after {ElapsedMilliseconds}ms",
                method,
                path,
                elapsedMilliseconds);

            _logger.LogError(
                exception,
                "HTTP {HttpMethod} {RequestPath}{QueryString} threw an exception after {ElapsedMilliseconds}ms [CorrelationId: {CorrelationId}]",
                method,
                path,
                queryString ?? string.Empty,
                elapsedMilliseconds,
                correlationId);
        }
        catch
        {
            // Ignore logging failures - we don't want to mask the original exception
        }

        // Always return false to ensure the exception is re-thrown
        return false;
    }

    /// <summary>
    /// Determines the appropriate log level based on the HTTP status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>
    /// Information for 2xx and 3xx status codes,
    /// Warning for 4xx status codes,
    /// Error for 5xx status codes.
    /// </returns>
    private static LogLevel DetermineLogLevel(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 400 => LogLevel.Information,  // Success (2xx) and Redirection (3xx)
            >= 400 and < 500 => LogLevel.Warning,       // Client errors (4xx)
            >= 500 => LogLevel.Error,                   // Server errors (5xx)
            _ => LogLevel.Information                   // Informational (1xx) or unknown
        };
    }

    /// <summary>
    /// Handles logging failures gracefully without breaking the request pipeline.
    /// </summary>
    /// <param name="exception">The exception that occurred during logging.</param>
    /// <param name="operation">The name of the logging operation that failed.</param>
    private static void HandleLoggingFailure(Exception exception, string operation)
    {
        // Use Debug.WriteLine as a last resort - this won't break the pipeline
        // and provides some visibility into logging issues during development
        Debug.WriteLine($"[RequestLoggingMiddleware] Logging failure in {operation}: {exception.Message}");

        // Optionally write to console in development scenarios
        try
        {
            Console.Error.WriteLine(
                $"[RequestLoggingMiddleware] Warning: Failed to log in {operation}. Error: {exception.Message}");
        }
        catch
        {
            // Ignore any console output failures
        }
    }
}

/// <summary>
/// Extension methods for registering RequestLoggingMiddleware in the ASP.NET Core pipeline.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds the request logging middleware to the application's request pipeline.
    /// </summary>
    /// <param name="builder">The <see cref="IApplicationBuilder"/> instance.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is null.
    /// </exception>
    /// <remarks>
    /// This middleware should be added early in the pipeline to capture timing 
    /// for the entire request processing. It is recommended to add it after
    /// exception handling middleware but before most other middleware.
    /// 
    /// Example usage in Program.cs:
    /// <code>
    /// app.UseExceptionHandling();
    /// app.UseRequestLogging();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// </code>
    /// </remarks>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
