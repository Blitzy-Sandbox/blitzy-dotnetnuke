namespace DnnMigration.Application.DTOs.Role;

// MIGRATION: Read DTO projected from RoleInfo.vb (DotNetNuke.Security.Roles). XML serialization
// attributes (<XmlRoot>/<XmlElement>/<XmlIgnore>) dropped; VB Single fees (ServiceFee, TrialFee) -> float;
// the Null.NullInteger (-1) "no role group" sentinel for RoleGroupID -> nullable int?; Null.NullString
// optionals -> string?. No EF navigation collections (this is the API anti-corruption boundary).

/// <summary>
/// Read/response DTO that is the full 1:1 projection of the <c>Role</c> domain entity. Returned by
/// <c>RolesController</c> (<c>GET /api/v1/roles</c>, <c>GET /api/v1/roles/{id}</c>) inside the uniform
/// <c>{ data, meta }</c> success envelope, and forms the anti-corruption boundary between the
/// <c>Role</c> entity and the REST API. Field shapes mirror the legacy <c>RoleInfo</c> value object 1:1.
/// </summary>
public class RoleDto
{
    public int RoleID { get; set; }
    public int PortalID { get; set; }
    public int? RoleGroupID { get; set; }
    public string? RoleName { get; set; }
    public string? Description { get; set; }
    // MIGRATION: schema columns [ServiceFee]/[TrialFee] money NULL and [TrialPeriod]/[BillingPeriod] int NULL
    // -> nullable CLR types so the legacy "no fee / no trial / no billing configured" (Null sentinel)
    // semantics round-trip as null instead of being coerced to 0.
    public float? ServiceFee { get; set; }
    public string? BillingFrequency { get; set; }
    public int? TrialPeriod { get; set; }
    public string? TrialFrequency { get; set; }
    public int? BillingPeriod { get; set; }
    public float? TrialFee { get; set; }
    public bool IsPublic { get; set; }
    public bool AutoAssignment { get; set; }
    public string? RSVPCode { get; set; }
    public string? IconFile { get; set; }
}
