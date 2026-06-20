using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Role-update validation parity with the RoleController.vb update path. Field rules are sourced
// from Website/admin/Security/EditRoles.ascx.vb (RoleName RequiredFieldValidator + cmdUpdate_Click numeric/
// frequency parsing); RoleInfo.vb declares no validation attributes, so these rules reproduce the legacy
// ASCX validators rather than entity-level metadata.
// MIGRATION: Error messages reproduce the legacy App_LocalResources/EditRoles.ascx.resx validator text
// verbatim (the resourcekey-bound text the CompareValidators actually display; the "<br>" presentation prefix
// is dropped for JSON/Angular delivery). Fee/period fields are nullable; FluentValidation comparison
// validators are skipped for null, preserving an unset value — matching the legacy CompareValidators, which
// only fire when their bound textbox is non-empty.
// MIGRATION: legacy EditRoles.ascx CompareValidator operators are reproduced EXACTLY (no relaxation):
// Service Fee and Trial Fee use Operator="GreaterThanEqual" ValueToCompare="0" (>= 0); Billing Period and
// Trial Period use Operator="GreaterThan" ValueToCompare="0" (> 0). DataTypeCheck "...Value Entered Is Not
// Valid" siblings are enforced upstream by the DTO's numeric typing (float?/int?).
// MIGRATION: Duplicate role-name detection (GetRoleByName) is stateful and handled in RoleService.
public class UpdateRoleValidator : AbstractValidator<UpdateRoleDto>
{
    private static readonly string[] ValidFrequencies = { "N", "O", "D", "W", "M", "Y" };

    public UpdateRoleValidator()
    {
        // MIGRATION: update targets an existing role - key must be valid (> 0). System roles
        // (Administrators/Registered Users) guardrails enforced in RoleService.
        RuleFor(x => x.RoleID)
            .GreaterThan(0).WithMessage("A valid Role is required");

        // MIGRATION: EditRoles.ascx.vb RoleName RequiredFieldValidator.
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("You Must Enter a Valid Name");

        // MIGRATION: EditRoles.ascx valServiceFee2 — Operator="GreaterThanEqual" ValueToCompare="0" (>= 0).
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0)
            .WithMessage("Service Fee Must Be Greater Than or Equal to Zero");
        // MIGRATION: EditRoles.ascx valTrialFee2 — Operator="GreaterThanEqual" ValueToCompare="0" (>= 0).
        RuleFor(x => x.TrialFee).GreaterThanOrEqualTo(0)
            .WithMessage("Trial Fee Must Be Greater Than or Equal to Zero");
        // MIGRATION: EditRoles.ascx valBillingPeriod2 — Operator="GreaterThan" ValueToCompare="0" (> 0).
        RuleFor(x => x.BillingPeriod).GreaterThan(0)
            .WithMessage("Billing Period Must Be Greater Than Zero");
        // MIGRATION: EditRoles.ascx valTrialPeriod2 — Operator="GreaterThan" ValueToCompare="0" (> 0).
        RuleFor(x => x.TrialPeriod).GreaterThan(0)
            .WithMessage("Trial Period Must Be Greater Than Zero");

        // MIGRATION: frequency from the "Frequency" list (N/O/D/W/M/Y, default N); checked only when supplied.
        RuleFor(x => x.BillingFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f)).WithMessage("Invalid Billing Frequency")
            .When(x => !string.IsNullOrEmpty(x.BillingFrequency));

        RuleFor(x => x.TrialFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f)).WithMessage("Invalid Trial Frequency")
            .When(x => !string.IsNullOrEmpty(x.TrialFrequency));
    }
}
