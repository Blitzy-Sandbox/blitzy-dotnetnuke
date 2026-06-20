using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Role-create validation parity. Rules from Website/admin/Security/EditRoles.ascx.vb
// (RoleName required; fee/period numeric; frequency list default "N"). RoleInfo.vb has no validation attributes.
// MIGRATION: Error messages reproduce the legacy App_LocalResources/EditRoles.ascx.resx validator text
// verbatim (the resourcekey-bound text the CompareValidators actually display; the "<br>" presentation
// prefix is dropped for JSON/Angular delivery). The fee/period fields are nullable (physical [Roles]
// money/int columns are NULL-able); FluentValidation comparison validators are skipped when the value is
// null, so an unset (null) fee/period is preserved and not rejected — matching the legacy CompareValidators,
// which only fire when their bound textbox is non-empty.
// MIGRATION: legacy EditRoles.ascx CompareValidator operators are reproduced EXACTLY (no relaxation):
// Service Fee and Trial Fee use Operator="GreaterThanEqual" ValueToCompare="0" (>= 0, so a zero/free fee is
// permitted); Billing Period and Trial Period use Operator="GreaterThan" ValueToCompare="0" (> 0, so a
// configured period must be at least 1). The DataTypeCheck "...Value Entered Is Not Valid" sibling validators
// are enforced upstream by the DTO's numeric typing (float?/int?), so only the range rules are reproduced here.
// MIGRATION: Duplicate role-name detection (GetRoleByName) is stateful and handled in RoleService.
public class CreateRoleValidator : AbstractValidator<CreateRoleDto>
{
    // MIGRATION: EditRoles.ascx.vb "Frequency" list values (N=None, O=one-time, D=day, W=week, M=month, Y=year).
    private static readonly string[] ValidFrequencies = { "N", "O", "D", "W", "M", "Y" };

    public CreateRoleValidator()
    {
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

        // MIGRATION: EditRoles.ascx.vb binds Billing/Trial frequency from the "Frequency" list (N/O/D/W/M/Y, default N); validated only when supplied.
        RuleFor(x => x.BillingFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f)).WithMessage("Invalid Billing Frequency")
            .When(x => !string.IsNullOrEmpty(x.BillingFrequency));

        RuleFor(x => x.TrialFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f)).WithMessage("Invalid Trial Frequency")
            .When(x => !string.IsNullOrEmpty(x.TrialFrequency));
    }
}
