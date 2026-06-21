namespace DnnMigration.Application.Common;

/// <summary>
/// Raised by an application service when a request is well-formed and authorized but cannot be
/// completed because it would violate a domain business rule — for example deleting the last
/// remaining portal, deleting a portal administrator, removing a protected user-role assignment, or
/// deleting a tab that still has child tabs. These conflicts map to HTTP <c>409 Conflict</c> in the
/// API's RFC 7807 error envelope, and the exception <see cref="System.Exception.Message"/> is a
/// deliberate, user-facing business message that is safe to surface to the client.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION (QA Finding F1-1): the application services previously signalled business-rule conflicts
/// with the general-purpose <see cref="System.InvalidOperationException"/>, and
/// <c>ExceptionHandlingMiddleware</c> blanket-mapped <em>every</em> <see cref="System.InvalidOperationException"/>
/// to <c>409 Conflict</c> while exposing its message unconditionally. That overload was unsafe:
/// EF Core surfaces database/transient infrastructure failures (a connection to the database server
/// could not be opened, a transient SQL fault, etc.) as <see cref="System.InvalidOperationException"/>
/// too. A real database outage was therefore misclassified as a <c>4xx</c> client error (so 5xx-based
/// monitoring/alerting and client retry/backoff never triggered) and its internal EF Core/provider
/// detail leaked to callers in Production (CWE-209).
/// </para>
/// <para>
/// Introducing this dedicated type lets the middleware map <em>only</em> a genuine business conflict to
/// <c>409</c>; unexpected/infrastructure exceptions (including EF Core's transient-failure
/// <see cref="System.InvalidOperationException"/> wrapper) are classified as server faults (<c>5xx</c>)
/// with their internal detail suppressed outside Development. The type intentionally derives directly
/// from <see cref="System.Exception"/> — <em>not</em> from <see cref="System.InvalidOperationException"/>
/// — so the two failure families can never again be conflated.
/// </para>
/// </remarks>
public sealed class BusinessConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessConflictException"/> class with a
    /// user-facing business message describing the conflict (surfaced verbatim as the RFC 7807
    /// <c>detail</c> of the resulting <c>409 Conflict</c> response).
    /// </summary>
    /// <param name="message">The safe, user-facing description of the business-rule conflict.</param>
    public BusinessConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessConflictException"/> class with a
    /// user-facing business message and the underlying cause.
    /// </summary>
    /// <param name="message">The safe, user-facing description of the business-rule conflict.</param>
    /// <param name="innerException">The exception that is the cause of this conflict, if any.</param>
    public BusinessConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
