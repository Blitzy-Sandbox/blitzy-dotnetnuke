namespace DnnMigration.Domain.Common;

// MIGRATION: Minimal replacement for the legacy DotNetNuke.Services.Exceptions.BasePortalException
// (Library/Components/Exceptions/BasePortalException.vb). The legacy <Serializable()> attribute,
// the ISerializable/GetObjectData machinery, the protected (SerializationInfo, StreamingContext)
// deserialization constructor, and the InitilizePrivateVariables() HTTP/portal/user runtime-context
// capture are intentionally NOT ported: binary serialization of exceptions is obsolete in .NET 8,
// and request/user/correlation context is now captured by the Api layer (CorrelationIdMiddleware +
// Serilog structured logging). DomainException is a fresh, dependency-free POCO exception thrown for
// domain-invariant violations; the Api layer's ExceptionHandlingMiddleware translates it to an
// RFC 7807 ProblemDetails response.
/// <summary>
/// Base exception type for domain-invariant violations within the DnnMigration domain.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with a specified
    /// error message describing the domain-invariant violation.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DomainException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
