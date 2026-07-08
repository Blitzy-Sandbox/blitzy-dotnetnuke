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
