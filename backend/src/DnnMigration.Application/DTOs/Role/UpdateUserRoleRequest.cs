namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Inbound payload to update (or cancel) a user-role assignment (PUT <c>/api/roles/assignments</c>).
/// Recomputes the assignment's expiry from the role's billing/trial schedule, or — when <see cref="Cancel"/>
/// is set — expires (paid+trial-used) or removes the assignment. Mirrors the legacy
/// RoleController.UpdateUserRole(PortalId, UserId, RoleId, Cancel) arguments.
/// </summary>
// MIGRATION: Source = Library/Components/Security/Roles/RoleController.vb UpdateUserRole (L472/L489). The legacy method
// recomputed ExpiryDate from the role's trial/billing Period + Frequency code (N/O/D/W/M/Y) and, on Cancel, either
// expired the role (when ServiceFee > 0 AndAlso IsTrialUsed) or deleted it. PortalId is the immutable multi-tenant
// scope (AAP 0.7.1), validated against the caller's JWT "portalId" claim by the controller (EnforceTenant).
public class UpdateUserRoleRequest
{
    // MIGRATION: RoleController.UpdateUserRole PortalId argument. Required; EnforceTenant matches it against the JWT
    // "portalId" claim so a portal admin can only update assignments within its own portal.
    public int PortalId { get; set; }

    // MIGRATION: RoleController.UpdateUserRole UserId argument.
    public int UserId { get; set; }

    // MIGRATION: RoleController.UpdateUserRole RoleId argument.
    public int RoleId { get; set; }

    // MIGRATION: RoleController.UpdateUserRole Cancel argument (the no-arg overload passed False). When true the legacy
    // code expired the assignment (ExpiryDate = yesterday) if ServiceFee > 0 AndAlso IsTrialUsed, otherwise deleted it.
    public bool Cancel { get; set; }
}
