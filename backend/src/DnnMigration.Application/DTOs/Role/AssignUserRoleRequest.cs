namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Inbound payload to assign a user to a role (POST <c>/api/roles/assignments</c>). Self-contained:
/// it carries the tenant scope (PortalId), the target user and role, and the optional effective/expiry
/// dates. Mirrors the legacy RoleController.AddUserRole arguments.
/// </summary>
// MIGRATION: Source = Library/Components/Security/Roles/RoleController.vb AddUserRole(PortalID, UserId, RoleId,
// [EffectiveDate,] ExpiryDate) (L277/L295) and the Website/admin/Security/SecurityRoles.ascx.vb user-role assignment
// grid. The legacy 4-argument overload defaulted EffectiveDate to DateTime.Now; here EffectiveDate is optional and the
// service applies the same DateTime.Now default when it is omitted. ExpiryDate is optional (the legacy Null.NullDate
// sentinel -> null, the migration's sentinel->nullable convention, AAP 0.5.1). PortalId is the immutable multi-tenant
// scope (AAP 0.7.1) and is validated against the caller's JWT "portalId" claim by the controller (EnforceTenant).
public class AssignUserRoleRequest
{
    // MIGRATION: tenant discriminator (RoleController.AddUserRole PortalID argument). Required; EnforceTenant matches it
    // against the JWT "portalId" claim so a portal admin can only assign roles within its own portal.
    public int PortalId { get; set; }

    // MIGRATION: RoleController.AddUserRole UserId argument — the user being enrolled.
    public int UserId { get; set; }

    // MIGRATION: RoleController.AddUserRole RoleId argument — the role being granted.
    public int RoleId { get; set; }

    // MIGRATION: RoleController.AddUserRole EffectiveDate argument. The 4-arg overload passed DateTime.Now; null here
    // means "use Now" (applied in RoleService.AssignUserRoleAsync). Null.NullDate -> null.
    public System.DateTime? EffectiveDate { get; set; }

    // MIGRATION: RoleController.AddUserRole ExpiryDate argument. Null = no expiry (legacy Null.NullDate -> null).
    public System.DateTime? ExpiryDate { get; set; }
}
