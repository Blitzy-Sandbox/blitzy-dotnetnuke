namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Inbound payload to update an existing role (PUT <c>/api/roles/{id}</c>). The RoleId
/// is route-bound and the PortalId tenant scope is immutable, so neither is included
/// here. The editable field set mirrors the legacy EditRoles.ascx.vb cmdUpdate_Click handler.
/// </summary>
// MIGRATION: Source = Website/admin/Security/EditRoles.ascx.vb (cmdUpdate_Click) + RoleInfo.vb (field types).
// MIGRATION: In the legacy edit screen RoleName was shown as a read-only label (txtRoleName.Visible=False)
//            yet cmdUpdate_Click still re-persisted RoleInfo.RoleName; the field is therefore retained here
//            to preserve identical update behavior (behavioral parity, AAP 0.7.1).
// MIGRATION: RoleId is route-bound; PortalId tenant scope is immutable on update (not part of the payload).
public class UpdateRoleRequest
{
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
