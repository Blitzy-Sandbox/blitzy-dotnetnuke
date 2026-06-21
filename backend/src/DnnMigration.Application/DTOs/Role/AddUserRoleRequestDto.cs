namespace DnnMigration.Application.DTOs.Role;

// MIGRATION (DEV-069): optional request body for POST /api/v1/roles/{roleId}/users/{userId}, restoring the
// legacy Website/admin/Security/SecurityRoles.ascx.vb membership-assignment inputs (EffectiveDate,
// ExpiryDate, and the "notify user" checkbox) that the first-cut REST contract dropped (the original
// AAP-mapped parity gap). The body is OPTIONAL (the controller declares EmptyBodyBehavior.Allow): a
// bodyless POST preserves the prior subscription-style assignment in which RoleService computes ExpiryDate
// from the role's trial/billing schedule (legacy RoleController.UpdateUserRole), whereas a supplied
// EffectiveDate and/or ExpiryDate performs a DIRECT operator-dated assignment, faithful to the legacy
// RoleController.AddUserRole(PortalId, UserId, RoleId, EffectiveDate, ExpiryDate) overload the admin screen
// invoked.
//
// MIGRATION: Notify reproduces the legacy "notify user" flag. The newsletter/bulk-email subsystem is
// explicitly OUT OF SCOPE (AAP 0.2.2 — "Newsletter / bulk-email beyond core user management" excluded), so
// there is no mail transport to invoke; Notify is accepted and recorded as a DOCUMENTED NO-OP rather than
// silently dropped from the contract, preserving the wire shape for a future mail integration. See
// MIGRATION_NOTES.md DEV-069.
//
// Date semantics carried over verbatim: a null EffectiveDate means "effective immediately" (legacy
// Null.NullDate), and a null ExpiryDate means "never expires". Under the API's default System.Text.Json
// camelCase policy the wire shape is { effectiveDate, expiryDate, notify }.
public class AddUserRoleRequestDto
{
    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool Notify { get; set; }
}
