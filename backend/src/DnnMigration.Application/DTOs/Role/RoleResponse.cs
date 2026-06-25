namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Read model for the <c>/api/roles</c> resource. Projected from the
/// <c>DnnMigration.Domain.Entities.Role</c> aggregate and mirrors all 15 legacy
/// RoleInfo fields. Returned inside the standard { data, meta } success envelope
/// and wrapped by Common.PagedResult of RoleResponse for list endpoints.
/// </summary>
// MIGRATION: Source = Library/Components/Security/Roles/RoleInfo.vb (XmlRoot "role", 15 props).
// MIGRATION: Plain read DTO — it MIRRORS the Role entity but never imports it (no raw entities exposed).
public record RoleResponse
{
    /// <summary>Role identifier (RoleInfo.RoleID).</summary>
    public int RoleId { get; init; }

    // MIGRATION: PortalId tenant discriminator preserved on the response (multi-tenant isolation, AAP 0.7.1).
    public int PortalId { get; init; }

    // MIGRATION: RoleInfo.RoleGroupID used the Null.NullInteger (-1) sentinel -> nullable int.
    public int? RoleGroupId { get; init; }

    /// <summary>Role name (RoleInfo.RoleName).</summary>
    public string RoleName { get; init; } = string.Empty;

    public string? Description { get; init; }

    // MIGRATION: RoleInfo.ServiceFee was VB Single -> kept as float to mirror the Role entity exactly.
    public float ServiceFee { get; init; }

    public string? BillingFrequency { get; init; }

    public int TrialPeriod { get; init; }

    public string? TrialFrequency { get; init; }

    public int BillingPeriod { get; init; }

    // MIGRATION: RoleInfo.TrialFee was VB Single -> kept as float to mirror the Role entity exactly.
    public float TrialFee { get; init; }

    public bool IsPublic { get; init; }

    public bool AutoAssignment { get; init; }

    // MIGRATION: Verbatim property name "RSVPCode" preserved from RoleInfo.RSVPCode (NOT "RsvpCode").
    public string? RSVPCode { get; init; }

    public string? IconFile { get; init; }
}
