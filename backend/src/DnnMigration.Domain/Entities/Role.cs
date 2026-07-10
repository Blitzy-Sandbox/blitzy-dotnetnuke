namespace DnnMigration.Domain.Entities;

/// <summary>
/// Security role entity.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Security.Roles.RoleInfo
// (Library/Components/Security/Roles/RoleInfo.vb, L42, XmlRoot "role"). VB `Single` -> `float`.
public class Role
{
    public int RoleID { get; set; }
    public int PortalID { get; set; }
    public int RoleGroupID { get; set; }

    // MIGRATION (QA finding - R10 Issue 14): the legacy DNN Roles table declares [RoleName] nvarchar(50) NOT
    // NULL (01.00.00.SqlDataProvider L117). That 50-character bound is enforced at the API boundary by the
    // FluentValidation MaximumLength(50) rule and pinned on the persistence side by RoleConfiguration
    // (HasMaxLength(50).IsRequired()); this property stays non-nullable to reflect the NOT NULL column.
    public string RoleName { get; set; } = string.Empty;

    // MIGRATION: The legacy Roles table (DotNetNuke.Schema.SqlDataProvider) declares Description,
    // BillingFrequency (char(1)), TrialFrequency (char(1)), RSVPCode and IconFile as NULLable columns.
    // These properties are therefore nullable reference types so EF Core maps them to NULLable columns
    // (matching the existing schema per AAP schema-fidelity) and does not treat them as required on
    // SaveChanges. The `= string.Empty` initializer is retained for in-memory-constructed defaults; EF
    // derives column nullability from the type annotation.
    public string? Description { get; set; } = string.Empty;
    public string? BillingFrequency { get; set; } = string.Empty;
    public float ServiceFee { get; set; }          // MIGRATION: VB Single -> float
    public string? TrialFrequency { get; set; } = string.Empty;
    public int TrialPeriod { get; set; }
    public int BillingPeriod { get; set; }
    public float TrialFee { get; set; }             // MIGRATION: VB Single -> float
    public bool IsPublic { get; set; }
    public bool AutoAssignment { get; set; }
    public string? RSVPCode { get; set; } = string.Empty;
    public string? IconFile { get; set; } = string.Empty;
}
