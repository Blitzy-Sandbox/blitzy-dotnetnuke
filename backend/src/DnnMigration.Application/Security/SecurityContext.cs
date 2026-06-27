namespace DnnMigration.Application.Security;

// MIGRATION: Replaces the ambient inputs the legacy PortalSecurity helpers read implicitly —
// UserController.GetCurrentUserInfo() (the current UserInfo, including its IsSuperUser flag and Roles collection)
// and HttpContext.Current.Request.IsAuthenticated (Library/Components/Security/PortalSecurity.vb L103-136). In the
// stateless BFF there is no ambient HttpContext or session; instead the authenticated principal's identity is
// carried in the JWT and projected into this explicit, immutable value object. The Api layer builds it from the
// ClaimsPrincipal (see ClaimsPrincipalSecurityExtensions.ToSecurityContext), so the permission evaluator stays a
// pure, easily-testable function of its inputs with no framework or HttpContext coupling.
//
// This preserves the legacy user -> role -> permission model exactly: Roles holds the role NAMES the principal is
// a member of (issued as ClaimTypes.Role claims by JwtService), IsSuperUser mirrors UserInfo.IsSuperUser, and
// IsAuthenticated mirrors Request.IsAuthenticated.
public sealed record SecurityContext
{
    /// <summary>
    /// The role names the principal is a member of (legacy <c>UserInfo.Roles</c>, issued as
    /// <c>ClaimTypes.Role</c> claims). Compared case-sensitively, matching the legacy <c>UserInfo.IsInRole</c>
    /// string comparison. Defaults to an empty set (no roles).
    /// </summary>
    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether the principal is a DNN host SuperUser (legacy <c>UserInfo.IsSuperUser</c>). A SuperUser short-
    /// circuits the permission-evaluator role checks exactly as in the legacy helpers.
    /// </summary>
    public bool IsSuperUser { get; init; }

    /// <summary>
    /// Whether the request is authenticated (legacy <c>HttpContext.Current.Request.IsAuthenticated</c>). Only an
    /// UNauthenticated principal can satisfy the "Unauthenticated Users" special role.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// The principal's user id (legacy <c>UserInfo.UserID</c>), used to evaluate direct per-user permission grants
    /// (the legacy "[userid]" role syntax). Null when no parseable user id is present on the principal.
    /// </summary>
    public int? UserId { get; init; }
}
