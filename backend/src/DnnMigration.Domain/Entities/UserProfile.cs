namespace DnnMigration.Domain.Entities;

/// <summary>
/// User profile value object. Composed by the <c>User</c> entity (as <c>User.Profile</c>).
/// </summary>
// NOTE: The composition target is referenced as inline code (&lt;c&gt;User&lt;/c&gt;) rather than a
// &lt;see cref="User"/&gt; link. The dependency-free Domain layer is compiled with
// GenerateDocumentationFile + warnings-as-errors, so an unresolved cref (CS1574) would fail the
// build whenever the sibling User entity is not part of the same compilation. This keeps the
// documentation intent while guaranteeing a clean build.
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserProfile
// (Library/Components/Users/Profile/UserProfile.vb, L41). In the legacy code these string
// properties were backed by a dynamic ProfilePropertyDefinitionCollection accessed via
// GetPropertyValue/SetProfileProperty; that DNN dynamic-profile infrastructure is out of scope,
// so they are exposed here as plain auto-properties. Downstream EF Core maps this to aspnet_Profile.
public class UserProfile
{
    public string Cell { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;

    // MIGRATION: legacy ReadOnly computed FullName = FirstName & " " & LastName
    public string FullName => $"{FirstName} {LastName}".Trim();

    public string IM { get; set; } = string.Empty;

    // MIGRATION: legacy ReadOnly dirty-tracking flag; DNN change-tracking infra dropped (EF Core
    // handles change tracking). Retained as a settable state flag for parity.
    public bool IsDirty { get; set; }

    public string LastName { get; set; } = string.Empty;

    // MIGRATION: legacy progressive-hydration flag; retained as plain state (no lazy-load logic).
    public bool ObjectHydrated { get; set; }

    public string PostalCode { get; set; } = string.Empty;
    public string PreferredLocale { get; set; } = string.Empty;

    // MIGRATION: legacy ReadOnly ProfilePropertyDefinitionCollection (out-of-scope DNN dynamic
    // profile system) simplified to a name->value dictionary to keep the Domain dependency-free.
    public Dictionary<string, string> ProfileProperties { get; set; } = new();

    public string Region { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public int TimeZone { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
}
