using DnnMigration.Domain.Enums;

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Definition (and current value) of a single dynamic user-profile property.
/// Elements of a <c>ProfilePropertyDefinitionCollection</c> exposed by <c>UserProfile.ProfileProperties</c>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Profile.ProfilePropertyDefinition
// (Library/Components/Users/Profile/ProfilePropertyDefinition.vb). Only the data/state-bearing
// members are retained (the legacy XML-serialization / validation attributes and the
// IPropertyAccess/IHydratable plumbing are dropped). Property names and CLR types mirror the
// legacy reference class so downstream EF Core mapping to the existing profile-property schema
// remains faithful. Behaviour (validation, hydration) lives in the Application/Infrastructure
// layers, not on this Domain value object.
public class ProfilePropertyDefinition
{
    // MIGRATION: legacy PropertyDefinitionId (Integer).
    public int PropertyDefinitionId { get; set; }

    // MIGRATION: legacy ModuleDefId (Integer).
    public int ModuleDefId { get; set; }

    // MIGRATION: legacy PortalId (Integer).
    public int PortalId { get; set; }

    // MIGRATION: legacy DataType (Integer) — references a list entry id, preserved as int.
    public int DataType { get; set; }

    // MIGRATION: legacy PropertyName (String).
    public string PropertyName { get; set; } = string.Empty;

    // MIGRATION: legacy PropertyCategory (String).
    public string PropertyCategory { get; set; } = string.Empty;

    // MIGRATION: legacy PropertyValue (String) — the current value for the owning user.
    public string PropertyValue { get; set; } = string.Empty;

    // MIGRATION: legacy DefaultValue (String).
    public string DefaultValue { get; set; } = string.Empty;

    // MIGRATION: legacy Length (Integer).
    public int Length { get; set; }

    // MIGRATION: legacy Required (Boolean).
    public bool Required { get; set; }

    // MIGRATION: legacy ValidationExpression (String).
    public string ValidationExpression { get; set; } = string.Empty;

    // MIGRATION: legacy ViewOrder (Integer).
    public int ViewOrder { get; set; }

    // MIGRATION: legacy Visible (Boolean).
    public bool Visible { get; set; }

    // MIGRATION: legacy Visibility (UserVisibilityMode).
    public UserVisibilityMode Visibility { get; set; } = UserVisibilityMode.AllUsers;

    // MIGRATION: legacy IsDirty is a ReadOnly change-tracking flag (Browsable(False), XmlIgnore).
    // Preserve read-only external access via a private setter; the DNN dynamic change-tracking
    // logic that toggled it is out of scope, so it defaults to false for a freshly-loaded property.
    public bool IsDirty { get; private set; }
}
