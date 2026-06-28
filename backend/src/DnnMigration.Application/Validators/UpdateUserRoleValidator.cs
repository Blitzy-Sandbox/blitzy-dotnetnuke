using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Synchronous request validator for the user-role UPDATE/CANCEL write (PUT /api/roles/assignments),
// transcribed from RoleController.UpdateUserRole (RoleController.vb L472/L489), which required the (PortalId, UserId,
// RoleId) triplet to identify the assignment to recompute or cancel. Behavioral parity (AAP 0.7.1): the identifier
// triplet must be present and positive; the expiry recomputation and Cancel semantics live in RoleService (multi-entity
// logic, not validation).
public sealed class UpdateUserRoleValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleValidator()
    {
        // MIGRATION: (QA F10 Issue #18, tenant-contract alignment) PortalId is the multi-tenant scope
        // (AAP 0.7.1) and must identify a real portal, i.e. >= 0. PortalId 0 is the VALID first/default DNN
        // portal -- the same contract every other validator enforces (CreateModuleValidator,
        // CreateUserValidator, ForgotPasswordValidator, LoginRequestValidator all use GreaterThanOrEqualTo(0)),
        // and the JWT "portalId" claim / Angular admin default is 0. The previous GreaterThan(0) rejected the
        // canonical portal context the rest of the stack uses. Aligned to >= 0 for cross-layer parity (mirrors
        // AssignUserRoleValidator so the assignment UPSERT/UPDATE share one PortalId contract).
        RuleFor(x => x.PortalId)
            .GreaterThanOrEqualTo(0).WithMessage("A valid Portal must be specified.");

        // MIGRATION: RoleController.UpdateUserRole UserId argument.
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("A valid User must be specified.");

        // MIGRATION: RoleController.UpdateUserRole RoleId argument.
        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("A valid Role must be specified.");
    }
}
