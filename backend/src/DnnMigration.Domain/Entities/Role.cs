namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Roles.RoleInfo
// (Library/Components/Security/Roles/RoleInfo.vb). Renamed RoleInfo -> Role; <XmlRoot>/<XmlElement>/<XmlIgnore>
// attributes removed; persistence-ignorant POCO. Portal-scoped (multi-tenant). VB Single -> C# float.
public class Role
{
    public int RoleId { get; set; }

    // MIGRATION: Multi-tenant discriminator (every role belongs to a portal).
    public int PortalId { get; set; }

    // MIGRATION: Optional FK to a role group (legacy RoleGroupID) -> nullable.
    public int? RoleGroupId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    // MIGRATION: VB Single -> C# float.
    public float ServiceFee { get; set; }

    public string? BillingFrequency { get; set; }

    public int TrialPeriod { get; set; }

    public string? TrialFrequency { get; set; }

    public int BillingPeriod { get; set; }

    // MIGRATION: VB Single -> C# float.
    public float TrialFee { get; set; }

    public bool IsPublic { get; set; }

    public bool AutoAssignment { get; set; }

    // MIGRATION: Legacy property name RSVPCode preserved verbatim.
    public string? RSVPCode { get; set; }

    public string? IconFile { get; set; }
}
