namespace DnnMigration.Domain.Entities;

/// <summary>
/// User identity entity. Composes <see cref="UserMembership"/> and <see cref="UserProfile"/>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserInfo
// (Library/Components/Users/UserInfo.vb, L41). The IPropertyAccess implementation (GetProperty /
// Cacheability) is dropped (out-of-scope DNN token infrastructure). The legacy progressive-hydration
// logic in the Membership/Profile/Roles getters (which called UserController/ProfileController/
// RoleController) is dropped — these are plain auto-properties now; population is a
// repository/service concern downstream.
public class User
{
    public int AffiliateID { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public bool IsSuperUser { get; set; }
    public string LastName { get; set; } = string.Empty;

    // MIGRATION: composition — legacy lazy-hydrated Membership property, now a plain owned object.
    public UserMembership Membership { get; set; } = new();

    public int PortalID { get; set; }

    // MIGRATION: composition — legacy lazy-hydrated Profile property, now a plain owned object.
    public UserProfile Profile { get; set; } = new();

    // MIGRATION: legacy String() array, lazy-hydrated from RoleController; now plain state.
    public string[] Roles { get; set; } = Array.Empty<string>();

    public int UserID { get; set; }

    // MIGRATION: legacy setter also set Membership.Username (side-effect); dropped for a clean POCO.
    public string Username { get; set; } = string.Empty;

    // MIGRATION: deprecated in DNN in favour of DisplayName; kept as a computed getter for parity.
    public string FullName => $"{FirstName} {LastName}".Trim();
}
