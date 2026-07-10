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

    // MIGRATION: legacy ReadOnly computed FullName returns `FirstName & " " & LastName`
    // (UserProfile.vb L203-207) with NO trimming. Preserve that exact behaviour — adding .Trim()
    // would change observable output when a name part is blank/whitespace and break behavioral
    // equivalence.
    public string FullName => $"{FirstName} {LastName}";

    public string IM { get; set; } = string.Empty;

    // MIGRATION: legacy IsDirty is a ReadOnly property backed by a private _IsDirty field
    // (UserProfile.vb L237-241), toggled only by internal profile-property mutation. Preserve the
    // read-only external contract via a private setter (external code must not mutate it). The DNN
    // change-tracking logic that toggled it is out of scope, so it defaults to false for a
    // freshly-loaded profile; EF Core performs actual persistence change tracking.
    public bool IsDirty { get; private set; }

    public string LastName { get; set; } = string.Empty;

    // MIGRATION: legacy progressive-hydration flag; retained as plain state (no lazy-load logic).
    public bool ObjectHydrated { get; set; }

    public string PostalCode { get; set; } = string.Empty;
    public string PreferredLocale { get; set; } = string.Empty;

    // MIGRATION: legacy ReadOnly ProfilePropertyDefinitionCollection (UserProfile.vb L329-336),
    // lazily initialized and never reassignable. Preserve BOTH the collection type and the
    // read-only contract: a getter-only auto-property initialized to an empty collection. Its
    // contents may be populated (mirroring the legacy lazy-populate of profile properties), but the
    // reference cannot be reassigned by external code. Keeping the faithful type/name preserves
    // schema fidelity for later EF Core mapping of the dynamic profile-property store.
    public ProfilePropertyDefinitionCollection ProfileProperties { get; } = new();

    public string Region { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public int TimeZone { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
}
