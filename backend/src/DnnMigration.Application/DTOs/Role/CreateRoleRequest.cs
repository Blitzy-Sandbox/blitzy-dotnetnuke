namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Inbound payload to create a role (POST <c>/api/roles</c>). The editable field set
/// is derived from the legacy EditRoles.ascx.vb cmdUpdate_Click handler. The
/// server-generated RoleId is intentionally omitted; PortalId carries the tenant scope.
/// </summary>
// MIGRATION: Source = Website/admin/Security/EditRoles.ascx.vb (cmdUpdate_Click) + RoleInfo.vb (field types).
// MIGRATION: No validation/business logic here — FluentValidation rules live in Application/Validators.
public class CreateRoleRequest
{
    // MIGRATION: PortalId tenant discriminator required on create (multi-tenant isolation, AAP 0.7.1).
    public int PortalId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    // MIGRATION: RoleGroupID used the Null.NullInteger (-1) sentinel -> nullable int.
    public int? RoleGroupId { get; set; }

    // MIGRATION: ServiceFee was VB Single -> float (mirrors Role entity).
    public float ServiceFee { get; set; }

    public string? BillingFrequency { get; set; }

    public int BillingPeriod { get; set; }

    // MIGRATION: TrialFee was VB Single -> float (mirrors Role entity).
    public float TrialFee { get; set; }

    public int TrialPeriod { get; set; }

    public string? TrialFrequency { get; set; }

    public bool IsPublic { get; set; }

    public bool AutoAssignment { get; set; }

    // MIGRATION: Verbatim property name "RSVPCode" preserved from RoleInfo.RSVPCode (NOT "RsvpCode").
    public string? RSVPCode { get; set; }

    public string? IconFile { get; set; }
}
