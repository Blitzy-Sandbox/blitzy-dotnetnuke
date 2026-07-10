namespace DnnMigration.Application.Exceptions;

/// <summary>
/// Signals that a requested operation conflicts with the current persisted state of a domain
/// resource and therefore cannot be completed. It is the Application-layer counterpart of
/// HTTP <c>409 Conflict</c>: the API's centralized <c>ExceptionHandlingMiddleware</c> maps this
/// exception to a <c>409 Conflict</c> RFC 7807 Problem Details response, surfacing
/// <see cref="System.Exception.Message"/> as the problem title/detail. The message is always a
/// caller-controlled, non-sensitive business statement (never raw system/stack information), so it
/// is safe to expose to clients even in Production.
/// </summary>
/// <remarks>
/// MIGRATION: the legacy DotNetNuke controllers expressed business-rule refusals imperatively rather
/// than through a typed error channel — for example <c>UserController.DeleteUser</c>
/// (Library/Components/Users/UserController.vb L200) refused to remove a portal administrator through
/// its <c>deleteAdmin</c> gate. The migrated stateless API needs an explicit, RFC 7807-consistent way
/// (AAP §0.7.1 "consistent RFC 7807 error responses") to reject an operation that is well-formed but
/// invalid given the current persisted state. This exception is that channel.
/// <para>
/// It is intentionally a distinct type (NOT <see cref="System.InvalidOperationException"/>) so the
/// middleware maps ONLY deliberate, business-rule conflicts to 409; an incidental framework
/// <see cref="System.InvalidOperationException"/> continues to surface as a 500, preserving the
/// existing safety-net semantics.
/// </para>
/// </remarks>
public sealed class ConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictException"/> class with a business-rule
    /// message that is safe to surface to API clients as the RFC 7807 problem title/detail.
    /// </summary>
    /// <param name="message">A non-sensitive, caller-controlled description of the conflict.</param>
    public ConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictException"/> class with a business-rule
    /// message and the underlying cause.
    /// </summary>
    /// <param name="message">A non-sensitive, caller-controlled description of the conflict.</param>
    /// <param name="innerException">The exception that triggered this conflict, if any.</param>
    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
