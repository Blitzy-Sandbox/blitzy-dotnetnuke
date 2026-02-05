// =============================================================================
// DnnMigration - ExceptionHandlingMiddleware
// ASP.NET Core middleware for global exception handling with RFC 7807 Problem Details
// =============================================================================
// This middleware catches all unhandled exceptions from downstream middleware
// and controllers, converting them to standardized RFC 7807 Problem Details
// JSON responses with appropriate HTTP status codes.
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DnnMigration.Api.Middleware;

/// <summary>
/// ASP.NET Core middleware for global exception handling with RFC 7807 Problem Details responses.
/// </summary>
/// <remarks>
/// <para>
/// This middleware intercepts all unhandled exceptions from downstream middleware and controllers,
/// converting them to standardized RFC 7807 Problem Details JSON responses. Exception types are
/// mapped to appropriate HTTP status codes:
/// </para>
/// <list type="bullet">
///     <item><description><see cref="ValidationException"/> → 400 Bad Request</description></item>
///     <item><description><see cref="NotFoundException"/> → 404 Not Found</description></item>
///     <item><description><see cref="UnauthorizedException"/> → 401 Unauthorized</description></item>
///     <item><description><see cref="ForbiddenException"/> → 403 Forbidden</description></item>
///     <item><description>Unhandled <see cref="Exception"/> → 500 Internal Server Error</description></item>
/// </list>
/// <para>
/// The middleware logs errors using <see cref="ILogger{TCategoryName}"/> at appropriate levels:
/// Error level for 5xx status codes and Warning level for 4xx status codes.
/// </para>
/// <para>
/// Stack traces and internal details are only included in responses when running in
/// the Development environment for security purposes.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Register in Program.cs:
/// app.UseExceptionHandling();
/// 
/// // Should be registered early in the pipeline to catch exceptions from all downstream middleware
/// </code>
/// </example>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate in the request pipeline.</param>
    /// <param name="logger">The logger instance for recording exception details.</param>
    /// <param name="environment">The web host environment for determining development mode.</param>
    /// <remarks>
    /// The middleware is constructed once and reused for all requests. The dependencies
    /// are injected via ASP.NET Core's dependency injection container.
    /// </remarks>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        
        // Configure JSON serialization with camelCase naming policy per RFC 7807
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Invokes the middleware to handle the HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method wraps the call to the next middleware in a try-catch block to intercept
    /// any unhandled exceptions. When an exception is caught, it is converted to an
    /// RFC 7807 Problem Details response with appropriate status code and error details.
    /// </para>
    /// <para>
    /// The response content type is set to 'application/problem+json' as specified by RFC 7807.
    /// </para>
    /// </remarks>
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
    /// Handles the caught exception by generating an RFC 7807 Problem Details response.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="exception">The exception that was caught.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Map exception type to HTTP status code and problem details
        var (statusCode, problemDetails) = MapExceptionToProblemDetails(exception);

        // Log the exception at the appropriate level based on status code
        LogException(exception, statusCode);

        // Set response properties per RFC 7807
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        // Serialize and write the Problem Details response
        var responseJson = JsonSerializer.Serialize(problemDetails, _jsonOptions);
        await context.Response.WriteAsync(responseJson);
    }

    /// <summary>
    /// Maps an exception to its corresponding HTTP status code and Problem Details object.
    /// </summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>A tuple containing the HTTP status code and Problem Details object.</returns>
    private (int StatusCode, ProblemDetails Details) MapExceptionToProblemDetails(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => CreateValidationProblemDetails(validationException),
            NotFoundException notFoundException => CreateNotFoundProblemDetails(notFoundException),
            UnauthorizedException unauthorizedException => CreateUnauthorizedProblemDetails(unauthorizedException),
            ForbiddenException forbiddenException => CreateForbiddenProblemDetails(forbiddenException),
            _ => CreateInternalServerErrorProblemDetails(exception)
        };
    }

    /// <summary>
    /// Creates Problem Details for a FluentValidation ValidationException (HTTP 400).
    /// </summary>
    /// <param name="exception">The validation exception containing validation errors.</param>
    /// <returns>A tuple with status code 400 and Problem Details including validation errors.</returns>
    private (int StatusCode, ProblemDetails Details) CreateValidationProblemDetails(ValidationException exception)
    {
        const int statusCode = StatusCodes.Status400BadRequest;
        
        // Extract validation errors from FluentValidation, grouped by property name
        // Uses LINQ GroupBy() and ToDictionary() as specified in external_imports
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => string.IsNullOrEmpty(g.Key) ? "_" : g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        var problemDetails = new ProblemDetails
        {
            Type = "https://dnnmigration.com/errors/validation",
            Title = "Validation Error",
            Status = statusCode,
            Detail = "One or more validation errors occurred.",
            Errors = errors
        };

        // Include stack trace only in development
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = exception.GetType().Name;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        return (statusCode, problemDetails);
    }

    /// <summary>
    /// Creates Problem Details for a NotFoundException (HTTP 404).
    /// </summary>
    /// <param name="exception">The not found exception with entity details.</param>
    /// <returns>A tuple with status code 404 and Problem Details.</returns>
    /// <remarks>
    /// Uses the EntityName and EntityKey properties from NotFoundException to provide
    /// detailed error information about which resource was not found.
    /// </remarks>
    private (int StatusCode, ProblemDetails Details) CreateNotFoundProblemDetails(NotFoundException exception)
    {
        const int statusCode = StatusCodes.Status404NotFound;

        var problemDetails = new ProblemDetails
        {
            Type = "https://dnnmigration.com/errors/not-found",
            Title = "Resource Not Found",
            Status = statusCode,
            Detail = exception.Message
        };

        // Include entity details from NotFoundException per internal_imports members_accessed
        if (exception.EntityName is not null)
        {
            problemDetails.Extensions["entityName"] = exception.EntityName;
        }

        if (exception.EntityKey is not null)
        {
            problemDetails.Extensions["entityKey"] = exception.EntityKey.ToString();
        }

        // Include stack trace only in development
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = exception.GetType().Name;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        return (statusCode, problemDetails);
    }

    /// <summary>
    /// Creates Problem Details for an UnauthorizedException (HTTP 401).
    /// </summary>
    /// <param name="exception">The unauthorized exception.</param>
    /// <returns>A tuple with status code 401 and Problem Details.</returns>
    private (int StatusCode, ProblemDetails Details) CreateUnauthorizedProblemDetails(UnauthorizedException exception)
    {
        const int statusCode = StatusCodes.Status401Unauthorized;

        var problemDetails = new ProblemDetails
        {
            Type = "https://dnnmigration.com/errors/unauthorized",
            Title = "Unauthorized",
            Status = statusCode,
            Detail = exception.Message
        };

        // Include stack trace only in development
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = exception.GetType().Name;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        return (statusCode, problemDetails);
    }

    /// <summary>
    /// Creates Problem Details for a ForbiddenException (HTTP 403).
    /// </summary>
    /// <param name="exception">The forbidden exception with permission details.</param>
    /// <returns>A tuple with status code 403 and Problem Details.</returns>
    /// <remarks>
    /// Uses the Permission property from ForbiddenException to provide information
    /// about which permission was required but not possessed by the user.
    /// </remarks>
    private (int StatusCode, ProblemDetails Details) CreateForbiddenProblemDetails(ForbiddenException exception)
    {
        const int statusCode = StatusCodes.Status403Forbidden;

        var problemDetails = new ProblemDetails
        {
            Type = "https://dnnmigration.com/errors/forbidden",
            Title = "Forbidden",
            Status = statusCode,
            Detail = exception.Message
        };

        // Include permission details from ForbiddenException per internal_imports members_accessed
        if (exception.Permission is not null)
        {
            problemDetails.Extensions["requiredPermission"] = exception.Permission;
        }

        // Include stack trace only in development
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = exception.GetType().Name;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        return (statusCode, problemDetails);
    }

    /// <summary>
    /// Creates Problem Details for an unhandled exception (HTTP 500).
    /// </summary>
    /// <param name="exception">The unhandled exception.</param>
    /// <returns>A tuple with status code 500 and Problem Details.</returns>
    /// <remarks>
    /// For security reasons, internal error details are never exposed in production.
    /// In development, the exception type, message, and stack trace are included
    /// to aid debugging.
    /// </remarks>
    private (int StatusCode, ProblemDetails Details) CreateInternalServerErrorProblemDetails(Exception exception)
    {
        const int statusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Type = "https://dnnmigration.com/errors/internal-server-error",
            Title = "Internal Server Error",
            Status = statusCode,
            // Never expose internal error details in production for security
            Detail = _environment.IsDevelopment() 
                ? exception.Message 
                : "An unexpected error occurred. Please try again later."
        };

        // Include detailed exception information only in development per security requirements
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = exception.GetType().FullName;
            problemDetails.Extensions["message"] = exception.Message;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;

            // Include inner exception details if present
            if (exception.InnerException is not null)
            {
                problemDetails.Extensions["innerException"] = new
                {
                    type = exception.InnerException.GetType().FullName,
                    message = exception.InnerException.Message,
                    stackTrace = exception.InnerException.StackTrace
                };
            }
        }

        return (statusCode, problemDetails);
    }

    /// <summary>
    /// Logs the exception at the appropriate level based on the HTTP status code.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="statusCode">The HTTP status code determined for the response.</param>
    /// <remarks>
    /// Per Section 0.7.6 requirements:
    /// - 5xx status codes are logged at Error level
    /// - 4xx status codes are logged at Warning level
    /// </remarks>
    private void LogException(Exception exception, int statusCode)
    {
        // Determine log level based on status code range per requirements
        // 5xx = Error, 4xx = Warning
        if (statusCode >= 500)
        {
            // Log at Error level for server errors (5xx)
            _logger.LogError(
                exception,
                "Unhandled exception occurred. Status: {StatusCode}, Type: {ExceptionType}, Message: {Message}",
                statusCode,
                exception.GetType().Name,
                exception.Message);
        }
        else
        {
            // Log at Warning level for client errors (4xx)
            _logger.LogWarning(
                exception,
                "Request failed with client error. Status: {StatusCode}, Type: {ExceptionType}, Message: {Message}",
                statusCode,
                exception.GetType().Name,
                exception.Message);
        }
    }
}

/// <summary>
/// RFC 7807 Problem Details object for standardized error responses.
/// </summary>
/// <remarks>
/// This class represents the RFC 7807 Problem Details specification for HTTP APIs.
/// All responses from the exception handling middleware use this format with
/// application/problem+json content type.
/// </remarks>
internal sealed class ProblemDetails
{
    /// <summary>
    /// Gets or sets a URI reference that identifies the problem type.
    /// </summary>
    /// <value>
    /// A URI reference (e.g., "https://dnnmigration.com/errors/validation")
    /// that provides human-readable documentation for the problem type.
    /// </value>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets a short, human-readable summary of the problem type.
    /// </summary>
    /// <value>
    /// A brief title describing the error category (e.g., "Validation Error", "Not Found").
    /// </value>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code generated by the origin server.
    /// </summary>
    /// <value>
    /// The HTTP status code (e.g., 400, 401, 403, 404, 500).
    /// </value>
    public int? Status { get; set; }

    /// <summary>
    /// Gets or sets a human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    /// <value>
    /// A detailed description of what went wrong in this specific request.
    /// </value>
    public string? Detail { get; set; }

    /// <summary>
    /// Gets or sets a URI reference that identifies the specific occurrence of the problem.
    /// </summary>
    /// <value>
    /// A URI that identifies the specific instance of this error (optional).
    /// </value>
    public string? Instance { get; set; }

    /// <summary>
    /// Gets or sets the validation errors dictionary for validation failures.
    /// </summary>
    /// <value>
    /// A dictionary mapping property names to arrays of error messages, 
    /// or null for non-validation errors.
    /// </value>
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Gets the extension members for additional problem details not covered by RFC 7807.
    /// </summary>
    /// <value>
    /// A dictionary for custom extension properties like entityName, entityKey,
    /// requiredPermission, exception details, and stack traces (development only).
    /// </value>
    [JsonExtensionData]
    public Dictionary<string, object?> Extensions { get; } = new();
}

/// <summary>
/// Extension methods for registering the <see cref="ExceptionHandlingMiddleware"/> in the ASP.NET Core pipeline.
/// </summary>
/// <remarks>
/// Provides a fluent extension method for <see cref="IApplicationBuilder"/> to register
/// the global exception handling middleware. Should be called early in the middleware
/// pipeline configuration to catch exceptions from all downstream middleware and controllers.
/// </remarks>
public static class ExceptionHandlingMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="ExceptionHandlingMiddleware"/> to the application's request pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This middleware should be registered early in the pipeline to catch exceptions from
    /// all downstream middleware and controllers. Example usage:
    /// </para>
    /// <code>
    /// var app = builder.Build();
    /// app.UseExceptionHandling();  // Register early
    /// app.UseHttpsRedirection();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapControllers();
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is null.
    /// </exception>
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
