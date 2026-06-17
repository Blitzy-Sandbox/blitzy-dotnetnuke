namespace DnnMigration.Application.Common;

// MIGRATION (F5-01 hardening): NEW construct introduced to close the QA FINAL F5 information-exposure
// finding (CWE-209). The Application services previously signalled business-rule conflicts (e.g.
// "Cannot delete the last remaining portal.") by throwing the FRAMEWORK type InvalidOperationException,
// which the API ExceptionHandlingMiddleware mapped to HTTP 409 by surfacing `exception.Message`
// UNCONDITIONALLY. EF Core (and other framework code) ALSO throws InvalidOperationException for
// transient/connection failures, so that 409 branch leaked ORM/framework internals to clients in
// Production. This dedicated type lets the middleware route ONLY deliberate, client-safe business
// conflicts to a 409-with-message, while a RAW InvalidOperationException falls through to the generic
// 500 branch whose detail is gated to Development. There is no legacy equivalent: the legacy
// *Controller.vb guards returned False / set a status string silently (e.g. PortalController "LastPortal",
// RoleController.DeleteUserRole, UserController admin guard); those returns were modernized into thrown
// exceptions in the CP services, and are now strengthened into this explicit, security-aware type.

/// <summary>
/// Signals a violated business rule that the caller could resolve — for example attempting to delete the
/// last remaining portal, remove the administrator from the administrator role, delete a tab that still has
/// child tabs, or delete the portal administrator. Instances of this type carry a message that is
/// <b>deliberately authored to be safe to return to clients</b>, so the API surfaces it as the
/// <c>detail</c> of an RFC 7807 <c>409 Conflict</c> Problem Details response in <em>all</em> environments.
/// </summary>
/// <remarks>
/// <para>
/// This type intentionally derives from <see cref="InvalidOperationException"/> for two reasons:
/// (1) it preserves the semantic that a business-rule conflict is an invalid operation on the current
/// state; and (2) it keeps the existing service-guard unit tests — which assert
/// <c>ThrowAsync&lt;InvalidOperationException&gt;()</c> (some also matching the message) — valid, since a
/// <see cref="BusinessRuleConflictException"/> <em>is</em> an <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// CRITICAL contract with <c>DnnMigration.Api.Middleware.ExceptionHandlingMiddleware</c>: the middleware
/// matches this <b>derived</b> type FIRST and returns a <c>409 Conflict</c> carrying <see cref="System.Exception.Message"/>.
/// A <b>raw</b> <see cref="InvalidOperationException"/> (e.g. an EF Core transient/connection failure) does
/// NOT match this case and instead falls through to the generic server-error branch, whose detail is gated
/// to <c>IHostEnvironment.IsDevelopment()</c> — so framework internals never leak in Production (CWE-209).
/// Therefore only messages that are safe for end users may be passed to this constructor.
/// </para>
/// </remarks>
public sealed class BusinessRuleConflictException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleConflictException"/> class with a
    /// client-safe message that will be returned verbatim as the RFC 7807 <c>409 Conflict</c> detail.
    /// </summary>
    /// <param name="message">
    /// A human-readable, <b>client-safe</b> description of the violated business rule. Because this value is
    /// surfaced to API clients in every environment, it must never contain secrets, connection details,
    /// stack traces, or other internal information.
    /// </param>
    public BusinessRuleConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleConflictException"/> class with a
    /// client-safe message and the exception that caused this conflict. The inner exception is preserved for
    /// server-side logging only and is never serialized into the client response.
    /// </summary>
    /// <param name="message">A human-readable, <b>client-safe</b> description of the violated business rule.</param>
    /// <param name="innerException">The underlying cause, retained for diagnostics.</param>
    public BusinessRuleConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
