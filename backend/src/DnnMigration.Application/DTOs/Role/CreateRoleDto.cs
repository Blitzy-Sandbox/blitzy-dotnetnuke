namespace DnnMigration.Application.DTOs.Role;

// MIGRATION: Create-request DTO derived from RoleInfo.vb (DotNetNuke.Security.Roles). Omits the
// server-assigned RoleID. RoleGroupID is INCLUDED (int?) because the legacy EditRoles.ascx.vb add path
// assigns a role group at creation (RoleController.AddRole receives the full RoleInfo). VB Single fees
// (ServiceFee, TrialFee) -> float; Null.NullInteger RoleGroupID sentinel -> int?; Null.NullString optionals
// -> string?. No EF navigation collections.
public class CreateRoleDto
{
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
