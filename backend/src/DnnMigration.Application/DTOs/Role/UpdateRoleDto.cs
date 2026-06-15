namespace DnnMigration.Application.DTOs.Role;

// MIGRATION: Update-request DTO derived from RoleInfo.vb (DotNetNuke.Security.Roles). Includes RoleID to
// identify the target role; PortalID retained for parity (legacy EditRoles.ascx.vb edit path / RoleController
// .UpdateRole re-set PortalID on the full RoleInfo). VB Single fees -> float; Null.NullInteger RoleGroupID
// sentinel -> int?; Null.NullString optionals -> string?. No EF navigation collections.
public class UpdateRoleDto
{
    public int RoleID { get; set; }
    public int PortalID { get; set; }
    public int? RoleGroupID { get; set; }
    public string? RoleName { get; set; }
    public string? Description { get; set; }
    public float ServiceFee { get; set; }
    public string? BillingFrequency { get; set; }
    public int TrialPeriod { get; set; }
    public string? TrialFrequency { get; set; }
    public int BillingPeriod { get; set; }
    public float TrialFee { get; set; }
    public bool IsPublic { get; set; }
    public bool AutoAssignment { get; set; }
    public string? RSVPCode { get; set; }
    public string? IconFile { get; set; }
}
