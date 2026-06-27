namespace DnnMigration.Domain.Entities;

// MIGRATION (CP-final review - profile workflow parity): per-portal profile property DEFINITION, converted from
// DotNetNuke.Entities.Profile.ProfilePropertyDefinition (Library/Components/Users/Profile/ProfilePropertyDefinition.vb).
// Code-First mapped to the EXISTING [ProfilePropertyDefinition] table (04.00.04.SqlDataProvider L1107); the schema is
// NOT altered. The DNN user profile is an EAV model: this table defines WHICH properties a portal exposes
// (FirstName, LastName, Cell, Telephone, ...) and their validation rules; the per-user VALUES live in [UserProfile]
// (see UserProfileValue). A property is resolved per portal by PropertyName (legacy GetProfilePropertyDefinitionID:
// SELECT PropertyDefinitionID FROM ProfilePropertyDefinition WHERE PortalID=@PortalID AND PropertyName=@PropertyName).
public class ProfilePropertyDefinition
{
    // MIGRATION: [ProfilePropertyDefinition].[PropertyDefinitionID] int IDENTITY(1,1) PK.
    public int PropertyDefinitionId { get; set; }

    // MIGRATION: [PortalID] - the multi-tenant discriminator; definitions are scoped per portal (AAP 0.7.1).
    public int PortalId { get; set; }

    // MIGRATION: [ModuleDefID] int NULL - the owning module definition (null for the core profile properties).
    public int? ModuleDefId { get; set; }

    // MIGRATION: [Deleted] bit - soft-delete flag; deleted definitions are excluded from the profile (legacy
    // GetPropertyDefinitionsByPortal filtered Deleted=0).
    public bool Deleted { get; set; }

    // MIGRATION: [DataType] int - the property data type id (preserved verbatim; not interpreted by this phase).
    public int DataType { get; set; }

    // MIGRATION: [DefaultValue] nvarchar(50) NULL - the value copied into a fresh profile (legacy
    // InitialiseProfile(useDefaults:=True) seeded each PropertyValue from DefaultValue).
    public string? DefaultValue { get; set; }

    // MIGRATION: [PropertyCategory] nvarchar(50) - the UI grouping category (e.g. "Name", "Address").
    public string PropertyCategory { get; set; } = string.Empty;

    // MIGRATION: [PropertyName] nvarchar(50) - the logical property name (e.g. "FirstName", "Cell"). The flat
    // UserProfileDto is mapped to/from these names (UserProfile.vb private constants cFirstName/cCell/...).
    public string PropertyName { get; set; } = string.Empty;

    // MIGRATION: [Length] int - maximum stored length for the value (0 = unlimited); part of legacy validation.
    public int Length { get; set; }

    // MIGRATION: [Required] bit - whether a value MUST be supplied; part of legacy profile validation.
    public bool Required { get; set; }

    // MIGRATION: [ValidationExpression] nvarchar(100) NULL - an optional regular expression the value must match
    // (legacy DNN rendered a RegularExpressionValidator from this); part of legacy profile validation.
    public string? ValidationExpression { get; set; }

    // MIGRATION: [ViewOrder] int - the display ordering of the property within its category.
    public int ViewOrder { get; set; }

    // MIGRATION: [Visible] bit - whether the property is shown on the profile UI.
    public bool Visible { get; set; }
}
