namespace DnnMigration.Domain.Enums;

/// <summary>
/// The SecurityAccessLevel enum is used to determine which level of access rights
/// to assign to a specific module or module action.
/// </summary>
// MIGRATION: Ported verbatim from legacy VB.NET enum DotNetNuke.Security.SecurityAccessLevel
// (Library/Components/Security/PortalSecurity.vb, lines 45-53), declared "As Integer".
// Exact integer backing values are preserved to keep the permission-comparison logic
// (legacy PortalSecurity.HasNecessaryPermission) behavior-equivalent (AAP 0.7.1).
public enum SecurityAccessLevel
{
    // MIGRATION: Negative sentinel values are intentional and preserved exactly from the legacy
    // enum. ControlPanel/SkinObject/Anonymous use negative ordinals (-3/-2/-1) that sort below the
    // positive permission tiers (View=0 .. Host=3). Do NOT renumber.
    ControlPanel = -3,
    SkinObject = -2,
    Anonymous = -1,
    View = 0,
    Edit = 1,
    Admin = 2,
    Host = 3,
}
