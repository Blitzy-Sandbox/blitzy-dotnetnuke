namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload used to provision a new portal record together with an optional
/// initial administrator user.
/// </summary>
/// <remarks>
/// Bound from the body of <c>POST /api/portals</c>. Validation rules for this payload are
/// owned by <c>PortalValidator</c> (FluentValidation) and entity projection is handled by
/// <c>MappingProfile</c> (AutoMapper); this type therefore intentionally carries no validation
/// attributes, no business logic, and no data-access concerns.
///
/// Portal descriptive fields (<see cref="PortalName"/>, <see cref="Description"/>,
/// <see cref="KeyWords"/>, <see cref="HomeDirectory"/>, <see cref="Email"/>) derive from the
/// legacy <c>PortalInfo</c> entity, and <see cref="PortalAlias"/> derives from the legacy
/// <c>PortalAliasInfo.HTTPAlias</c> field. The administrator credential fields
/// (<see cref="FirstName"/>, <see cref="LastName"/>, <see cref="Username"/>,
/// <see cref="Password"/>) model the optional initial admin user.
/// </remarks>
// MIGRATION: legacy template/file-system provisioning (TemplatePath/ParseTemplate/ProcessResourceFile) is out of scope; this DTO provisions a portal record + optional admin user only.
public record CreatePortalDto
{
    /// <summary>Display name of the portal to create. Required.</summary>
    public string PortalName { get; init; } = string.Empty;

    /// <summary>First name of the initial administrator user. Required.</summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Last name of the initial administrator user. Required.</summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>Login name of the initial administrator user. Required.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Plain-text password for the initial administrator user; hashed by the service layer. Required.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Optional password confirmation supplied by the UI purely for the match check performed in
    /// <c>PortalValidator</c>. MIGRATION: signup.ascx <c>txtConfirm</c>.
    /// </summary>
    public string? ConfirmPassword { get; init; }

    /// <summary>Email address of the initial administrator user. Required.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Optional free-text description of the portal.</summary>
    public string? Description { get; init; }

    /// <summary>Optional comma/space separated keywords describing the portal.</summary>
    public string? KeyWords { get; init; }

    /// <summary>Optional home directory path for portal-scoped storage.</summary>
    public string? HomeDirectory { get; init; }

    /// <summary>Initial HTTP alias (host name) for the portal. Required.</summary>
    public string PortalAlias { get; init; } = string.Empty;
}
