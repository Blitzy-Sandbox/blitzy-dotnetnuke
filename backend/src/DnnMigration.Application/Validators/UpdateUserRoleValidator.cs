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
        // MIGRATION: PortalId is the multi-tenant scope (AAP 0.7.1) and must identify a real portal (> 0).
        RuleFor(x => x.PortalId)
            .GreaterThan(0).WithMessage("A valid Portal must be specified.");

        // MIGRATION: RoleController.UpdateUserRole UserId argument.
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("A valid User must be specified.");

        // MIGRATION: RoleController.UpdateUserRole RoleId argument.
        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("A valid Role must be specified.");
    }
}
