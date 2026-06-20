namespace DnnMigration.Application.DTOs.Role;

// MIGRATION: Read/response DTO projected 1:1 from the legacy RoleInfo.vb value object
// (VB class RoleInfo, decorated <XmlRoot("role")>, in Namespace DotNetNuke.Security.Roles;
// the 15 backing fields are declared at Library/Components/Security/Roles/RoleInfo.vb L43-57).
// This type is the API anti-corruption boundary between the Role domain entity
// (DnnMigration.Domain.Entities.Role) and the REST surface: it is the payload returned by
// RolesController for GET /api/v1/roles and GET /api/v1/roles/{id} inside the uniform
// { data, meta } success envelope. Migration decisions encoded here (Minimal Change Clause):
//   * Legacy XML serialization attributes (<XmlRoot>/<XmlElement>/<XmlIgnore>) are dropped;
//     serialization is owned by the API layer, not the DTO.
//   * VB Single fees (ServiceFee, TrialFee) -> 32-bit C# float for precision parity (NOT double/decimal).
//   * The Null.NullInteger (= -1) "no role group" sentinel for RoleGroupID -> nullable int? at the boundary.
//   * Null.NullString (= "") optional strings -> nullable string?.
//   * No EF navigation collections, no DataAnnotations/attributes, no methods/constructors: this is a
//     pure POCO projection. Validation lives in the sibling FluentValidation validators and entity<->DTO
//     mapping lives in the sibling AutoMapper RoleProfile.
public class RoleDto
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
