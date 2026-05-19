// =============================================================================
// DnnMigration - UnauthorizedException
// Custom application exception for authentication failures (HTTP 401)
// =============================================================================
// This exception is thrown by application services when user authentication is
// required but not provided or invalid. It is caught by ExceptionHandlingMiddleware
// and converted to an RFC 7807 Problem Details response with 401 status code.
// =============================================================================

namespace DnnMigration.Application.Exceptions;

/// <summary>
/// Exception thrown when user authentication is required but not provided or invalid.
/// </summary>
/// <remarks>
/// <para>
/// This exception is used throughout the application layer when service methods
/// require authenticated access but the caller has not provided valid credentials.
/// Common scenarios include:
/// </para>
/// <list type="bullet">
///     <item><description>Missing authentication token</description></item>
///     <item><description>Expired authentication token</description></item>
///     <item><description>Invalid or malformed credentials</description></item>
///     <item><description>Failed credential validation</description></item>
/// </list>
/// <para>
/// The <c>ExceptionHandlingMiddleware</c> in the API layer catches this exception and
/// converts it to an RFC 7807 Problem Details response with HTTP 401 Unauthorized status.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Usage in a service method:
/// if (currentUser is null)
/// {
///     throw new UnauthorizedException();
/// }
/// 
/// // Usage with custom message:
/// if (!await ValidateTokenAsync(token))
/// {
///     throw new UnauthorizedException("The provided authentication token is invalid or expired.");
/// }
/// </code>
/// </example>
public class UnauthorizedException : Exception
{
    /// <summary>
    /// The default error message used when no custom message is specified.
    /// </summary>
    private const string DefaultMessage = "Authentication is required.";

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedException"/> class.
    /// </summary>
    /// <remarks>
    /// Creates an UnauthorizedException with the default message "Authentication is required."
    /// This is the most common constructor used when authentication is simply missing.
    /// </remarks>
    public UnauthorizedException()
        : base(DefaultMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <remarks>
    /// Use this constructor when you want to provide a more specific error message
    /// explaining why authentication failed.
    /// </remarks>
    /// <example>
    /// <code>
    /// throw new UnauthorizedException("The authentication token has expired.");
    /// </code>
    /// </example>
    public UnauthorizedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedException"/> class
    /// with a specified error message and a reference to the inner exception
    /// that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or <c>null</c>
    /// if no inner exception is specified.
    /// </param>
    /// <remarks>
    /// Use this constructor when wrapping another exception that caused the
    /// authentication failure. This preserves the original exception details
    /// for debugging while presenting a clean authentication error to the client.
    /// </remarks>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     await ValidateCredentialsAsync(username, password);
    /// }
    /// catch (CryptographicException ex)
    /// {
    ///     throw new UnauthorizedException("Credential validation failed.", ex);
    /// }
    /// </code>
    /// </example>
    public UnauthorizedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
