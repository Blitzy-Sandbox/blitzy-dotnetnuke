// =============================================================================
// DnnMigration - ForbiddenException
// Custom application exception for authorization failures (HTTP 403)
// =============================================================================
// This exception is thrown by application services when a user is authenticated
// but lacks the necessary permissions for the requested operation. It is caught
// by ExceptionHandlingMiddleware and converted to an RFC 7807 Problem Details
// response with 403 status code.
// =============================================================================

namespace DnnMigration.Application.Exceptions;

/// <summary>
/// Exception thrown when an authenticated user lacks permission to perform a requested operation.
/// </summary>
/// <remarks>
/// <para>
/// This exception is used throughout the application layer when service methods
/// determine that the authenticated user does not have the required permissions
/// to execute the requested action. Unlike <see cref="UnauthorizedException"/>,
/// this exception indicates that the user's identity is known (authenticated),
/// but they lack authorization for the specific resource or operation.
/// </para>
/// <para>
/// The <c>ExceptionHandlingMiddleware</c> in the API layer catches this exception and
/// converts it to an RFC 7807 Problem Details response with HTTP 403 Forbidden status.
/// </para>
/// <para>
/// The optional <see cref="Permission"/> property identifies the specific permission
/// that was required but not possessed by the user, enabling detailed error reporting.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Usage in a service method:
/// if (!await _permissionService.HasPermissionAsync(userId, "ADMIN_PORTALS"))
/// {
///     throw new ForbiddenException("ADMIN_PORTALS");
/// }
/// 
/// // Usage with custom message:
/// if (!user.IsSuperUser)
/// {
///     throw new ForbiddenException("Only super users can perform this operation.");
/// }
/// </code>
/// </example>
public class ForbiddenException : Exception
{
    /// <summary>
    /// Gets the name of the permission that was required for the operation.
    /// </summary>
    /// <value>
    /// The permission name (e.g., "ADMIN_PORTALS", "EDIT_USERS", "MANAGE_MODULES"),
    /// or <c>null</c> if not specified.
    /// </value>
    /// <remarks>
    /// This property is used to construct meaningful error messages that identify
    /// which specific permission the user was lacking. It can be used by the
    /// ExceptionHandlingMiddleware to include permission details in the error response.
    /// </remarks>
    public string? Permission { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class.
    /// </summary>
    /// <remarks>
    /// Creates a ForbiddenException with a default message indicating access was denied.
    /// </remarks>
    public ForbiddenException()
        : base("Access denied.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <remarks>
    /// Use this constructor when you want to provide a custom error message
    /// without specifying permission details.
    /// </remarks>
    public ForbiddenException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class
    /// with a specified error message and a reference to the inner exception
    /// that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or <c>null</c>
    /// if no inner exception is specified.
    /// </param>
    /// <remarks>
    /// Use this constructor when wrapping another exception that led to the
    /// forbidden condition.
    /// </remarks>
    public ForbiddenException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class
    /// with the permission name that was required but not possessed.
    /// </summary>
    /// <param name="permission">
    /// The name of the permission required for the operation (e.g., "ADMIN_PORTALS", "EDIT_USERS").
    /// </param>
    /// <param name="includePermissionInMessage">
    /// When <c>true</c>, includes the permission name in the exception message.
    /// Default is <c>true</c>.
    /// </param>
    /// <remarks>
    /// <para>
    /// This is the recommended constructor for service layer usage when authorization
    /// checks fail. It provides context about which permission was required.
    /// </para>
    /// <para>
    /// Example: <c>throw new ForbiddenException("ADMIN_PORTALS");</c>
    /// will generate the message: "Access denied. Required permission: 'ADMIN_PORTALS'."
    /// </para>
    /// </remarks>
    public ForbiddenException(string permission, bool includePermissionInMessage)
        : base(includePermissionInMessage 
            ? $"Access denied. Required permission: '{permission}'." 
            : "Access denied.")
    {
        Permission = permission;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class
    /// with the permission name and a custom message.
    /// </summary>
    /// <param name="permission">
    /// The name of the permission required for the operation (e.g., "ADMIN_PORTALS", "EDIT_USERS").
    /// </param>
    /// <param name="message">A custom error message.</param>
    /// <remarks>
    /// Use this constructor when you need to provide a custom message while still
    /// capturing the permission name for error handling purposes.
    /// </remarks>
    public ForbiddenException(string permission, string message)
        : base(message)
    {
        Permission = permission;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class
    /// with the permission name, custom message, and inner exception.
    /// </summary>
    /// <param name="permission">
    /// The name of the permission required for the operation (e.g., "ADMIN_PORTALS", "EDIT_USERS").
    /// </param>
    /// <param name="message">A custom error message.</param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or <c>null</c>
    /// if no inner exception is specified.
    /// </param>
    /// <remarks>
    /// This constructor provides complete flexibility for exception creation,
    /// allowing specification of all properties including the inner exception
    /// for exception chaining scenarios.
    /// </remarks>
    public ForbiddenException(string permission, string message, Exception? innerException)
        : base(message, innerException)
    {
        Permission = permission;
    }

    /// <summary>
    /// Creates a <see cref="ForbiddenException"/> for a specific permission requirement.
    /// </summary>
    /// <param name="permission">The name of the required permission.</param>
    /// <returns>A new <see cref="ForbiddenException"/> instance with the permission set.</returns>
    /// <remarks>
    /// Factory method providing a fluent way to create permission-based forbidden exceptions.
    /// </remarks>
    /// <example>
    /// <code>
    /// throw ForbiddenException.ForPermission("ADMIN_PORTALS");
    /// </code>
    /// </example>
    public static ForbiddenException ForPermission(string permission)
    {
        return new ForbiddenException(permission, includePermissionInMessage: true);
    }

    /// <summary>
    /// Creates a <see cref="ForbiddenException"/> for a resource-based authorization failure.
    /// </summary>
    /// <param name="resourceType">The type of resource being accessed (e.g., "Portal", "Module").</param>
    /// <param name="resourceId">The identifier of the resource.</param>
    /// <param name="operation">The operation being attempted (e.g., "Edit", "Delete").</param>
    /// <returns>A new <see cref="ForbiddenException"/> instance with a descriptive message.</returns>
    /// <remarks>
    /// Factory method for creating forbidden exceptions with resource-specific context.
    /// </remarks>
    /// <example>
    /// <code>
    /// throw ForbiddenException.ForResource("Portal", portalId, "Delete");
    /// </code>
    /// </example>
    public static ForbiddenException ForResource(string resourceType, object resourceId, string operation)
    {
        return new ForbiddenException(
            $"{operation}_{resourceType}".ToUpperInvariant(),
            $"Access denied. You do not have permission to {operation.ToLowerInvariant()} {resourceType} with ID '{resourceId}'.");
    }
}
