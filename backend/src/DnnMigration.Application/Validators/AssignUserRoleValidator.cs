using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Synchronous request validator for the user-role ASSIGNMENT write (POST /api/roles/assignments),
// transcribed from the Website/admin/Security/SecurityRoles.ascx user-role assignment grid (a user and a role must be
// selected, scoped to the current portal). Behavioral parity (AAP 0.7.1): the identifier triplet must be present and
// positive. Existence / tenant-ownership of the user and role are multi-entity concerns enforced in RoleService
// (PORTAL-SCOPED lookups), not here (no repository/data access in a FluentValidation validator).
public sealed class AssignUserRoleValidator : AbstractValidator<AssignUserRoleRequest>
{
    public AssignUserRoleValidator()
    {
        // MIGRATION: (QA F10 Issue #18, tenant-contract alignment) PortalId is the multi-tenant scope
        // (AAP 0.7.1) and must identify a real portal, i.e. >= 0. PortalId 0 is the VALID first/default DNN
        // portal -- the same contract every other validator enforces (CreateModuleValidator,
        // CreateUserValidator, ForgotPasswordValidator, LoginRequestValidator all use GreaterThanOrEqualTo(0)),
        // and the JWT "portalId" claim / Angular admin default is 0. The previous GreaterThan(0) rejected the
        // canonical portal context the rest of the stack uses, so an assignment for portal 0 failed with
        // "A valid Portal must be specified." before reaching RoleService. Aligned to >= 0 for cross-layer parity.
        RuleFor(x => x.PortalId)
            .GreaterThanOrEqualTo(0).WithMessage("A valid Portal must be specified.");

        // MIGRATION: SecurityRoles.ascx required a user selection before assignment.
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("A valid User must be specified.");

        // MIGRATION: SecurityRoles.ascx required a role selection before assignment.
        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("A valid Role must be specified.");
    }
}
