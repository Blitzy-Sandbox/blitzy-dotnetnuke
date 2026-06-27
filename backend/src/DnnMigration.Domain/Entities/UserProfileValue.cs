namespace DnnMigration.Domain.Entities;

// MIGRATION (CP-final review - profile workflow parity): one per-user profile VALUE row (an EAV cell), converted
// from the storage backing DotNetNuke.Entities.Users.UserProfile (Library/Components/Users/Profile/UserProfile.vb).
// Code-First mapped to the EXISTING [UserProfile] table (04.00.04.SqlDataProvider L1411); the schema is NOT altered.
// Each row is the value of one ProfilePropertyDefinition for one user: legacy GetPropertyValue(name) resolved the
// definition by (PortalID, PropertyName) and returned this row's PropertyValue; SetProfileProperty(name, value)
// wrote it. The flat UserProfileDto is a projection over these rows keyed by the definition's PropertyName.
// (Named UserProfileValue rather than "UserProfile" to keep the EAV row entity distinct from the flat DTO.)
public class UserProfileValue
{
    // MIGRATION: [UserProfile].[ProfileID] int IDENTITY(1,1) PK.
    public int ProfileId { get; set; }

    // MIGRATION: [UserID] int - the owning user (FK -> Users, ON DELETE CASCADE in the legacy schema).
    public int UserId { get; set; }

    // MIGRATION: [PropertyDefinitionID] int - the property this value is for (FK -> ProfilePropertyDefinition,
    // ON DELETE CASCADE). Resolved per portal by PropertyName.
    public int PropertyDefinitionId { get; set; }

    // MIGRATION: [PropertyValue] nvarchar(3750) NULL - the stored value (legacy GetPropertyValue returned this;
    // Null.NullString when absent). TimeZone was stored here as the Integer's ToString (UserProfile.vb L406-420).
    public string? PropertyValue { get; set; }

    // MIGRATION: [PropertyText] ntext NULL - the large-text overflow value (unused by the core flat properties;
    // mapped for schema fidelity so EF never references a missing column).
    public string? PropertyText { get; set; }

    // MIGRATION: [Visibility] int NOT NULL DEFAULT 0 - the per-user visibility setting for the property value.
    public int Visibility { get; set; }

    // MIGRATION: [LastUpdatedDate] datetime NOT NULL - stamped on every write (legacy UpdateUserProfile set it).
    public DateTime LastUpdatedDate { get; set; }
}
