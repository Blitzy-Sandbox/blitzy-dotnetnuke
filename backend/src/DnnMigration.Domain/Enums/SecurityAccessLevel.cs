namespace DnnMigration.Domain.Enums;

/// <summary>
/// The <see cref="SecurityAccessLevel"/> enum is used to determine which level of access rights
/// to assign to a specific module or module action.
/// </summary>
// MIGRATION: Converted verbatim from the DotNetNuke VB.NET `Public Enum SecurityAccessLevel As Integer`
// in Library/Components/Security/PortalSecurity.vb (L45). All 7 members and their explicit integer
// values — including the negative ControlPanel = -3, SkinObject = -2, and Anonymous = -1 — are
// preserved exactly because these values are a persistence/wire contract mapped to existing integer
// columns by EF Core downstream. The behavioral logic that consumed this enum in the legacy
// PortalSecurity class (e.g., HasNecessaryPermission(...) checks, DES encryption, and
// FormsAuthentication.SignOut) is NOT ported into the Domain layer; it is relocated to the
// Application/Api authentication layer (AuthService / AuthController) as JWT bearer authentication
// with [Authorize]/[AllowAnonymous] role/claims policies. Only the enum contract lives here.
public enum SecurityAccessLevel
{
    ControlPanel = -3,
    SkinObject = -2,
    Anonymous = -1,
    View = 0,
    Edit = 1,
    Admin = 2,
    Host = 3
}
