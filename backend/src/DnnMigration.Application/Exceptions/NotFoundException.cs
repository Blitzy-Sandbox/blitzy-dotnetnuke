// =============================================================================
// DnnMigration - NotFoundException
// Custom application exception for resource not found scenarios (HTTP 404)
// =============================================================================
// This exception is thrown by application services when a requested entity
// cannot be found in the data store. It is caught by ExceptionHandlingMiddleware
// and converted to an RFC 7807 Problem Details response with 404 status code.
// =============================================================================

namespace DnnMigration.Application.Exceptions;

/// <summary>
/// Exception thrown when a requested entity or resource cannot be found.
/// </summary>
/// <remarks>
/// <para>
/// This exception is used throughout the application layer when service methods
/// attempt to retrieve entities that do not exist. The exception includes
/// information about the entity type and key value to facilitate detailed
/// error reporting.
/// </para>
/// <para>
/// The <c>ExceptionHandlingMiddleware</c> in the API layer catches this exception and
/// converts it to an RFC 7807 Problem Details response with HTTP 404 status.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Usage in a service method:
/// var portal = await _portalRepository.GetByIdAsync(id);
/// if (portal is null)
/// {
///     throw new NotFoundException(nameof(Portal), id);
/// }
/// </code>
/// </example>
public class NotFoundException : Exception
{
    /// <summary>
    /// Gets the name of the entity type that was not found.
    /// </summary>
    /// <value>
    /// The entity type name (e.g., "Portal", "User", "Module"), or <c>null</c>
    /// if not specified.
    /// </value>
    /// <remarks>
    /// This property is used to construct meaningful error messages that identify
    /// which type of resource the client attempted to access.
    /// </remarks>
    public string? EntityName { get; }

    /// <summary>
    /// Gets the key or identifier of the entity that was not found.
    /// </summary>
    /// <value>
    /// The entity key value (typically the primary key), or <c>null</c> if not specified.
    /// </value>
    /// <remarks>
    /// This property stores the identifier used in the lookup operation. It is stored
    /// as an <see cref="object"/> to support various key types (int, Guid, string, etc.).
    /// </remarks>
    public object? EntityKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    /// <remarks>
    /// Creates a NotFoundException with a default message indicating a resource was not found.
    /// </remarks>
    public NotFoundException()
        : base("The requested resource was not found.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <remarks>
    /// Use this constructor when you want to provide a custom error message
    /// without specifying entity details.
    /// </remarks>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class
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
    /// not found condition.
    /// </remarks>
    public NotFoundException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class
    /// with the entity name and key that could not be found.
    /// </summary>
    /// <param name="entityName">
    /// The name of the entity type that was not found (e.g., "Portal", "User").
    /// </param>
    /// <param name="entityKey">
    /// The key or identifier used to look up the entity.
    /// </param>
    /// <remarks>
    /// <para>
    /// This is the recommended constructor for service layer usage as it provides
    /// complete context about the missing resource. The message is automatically
    /// generated to include both the entity name and key value.
    /// </para>
    /// <para>
    /// Example: <c>throw new NotFoundException(nameof(Portal), portalId);</c>
    /// will generate the message: "Entity 'Portal' with key '42' was not found."
    /// </para>
    /// </remarks>
    public NotFoundException(string entityName, object entityKey)
        : base($"Entity '{entityName}' with key '{entityKey}' was not found.")
    {
        EntityName = entityName;
        EntityKey = entityKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class
    /// with the entity name, key, and a custom message.
    /// </summary>
    /// <param name="entityName">
    /// The name of the entity type that was not found (e.g., "Portal", "User").
    /// </param>
    /// <param name="entityKey">
    /// The key or identifier used to look up the entity.
    /// </param>
    /// <param name="message">A custom error message.</param>
    /// <remarks>
    /// Use this constructor when you need to provide a custom message while still
    /// capturing the entity name and key for error handling purposes.
    /// </remarks>
    public NotFoundException(string entityName, object entityKey, string message)
        : base(message)
    {
        EntityName = entityName;
        EntityKey = entityKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class
    /// with the entity name, key, custom message, and inner exception.
    /// </summary>
    /// <param name="entityName">
    /// The name of the entity type that was not found (e.g., "Portal", "User").
    /// </param>
    /// <param name="entityKey">
    /// The key or identifier used to look up the entity.
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
    public NotFoundException(string entityName, object entityKey, string message, Exception? innerException)
        : base(message, innerException)
    {
        EntityName = entityName;
        EntityKey = entityKey;
    }
}
