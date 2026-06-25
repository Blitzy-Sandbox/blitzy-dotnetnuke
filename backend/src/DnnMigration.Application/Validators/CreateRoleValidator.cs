using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Security/EditRoles.ascx (valRoleName RequiredFieldValidator + the
// Service Fee / Billing Period / Trial Fee / Trial Period CompareValidators) and EditRoles.ascx.vb. Behavioral parity
// (AAP 0.7.1): identical required-ness, range checks and error strings. Synchronous-only validator (no repository/
// data access): the following are multi-entity concerns enforced in RoleService, not here --
//   * Duplicate-role-name detection (EditRoles.ascx.vb L251-258, create-only GetRoleByName -> "A role with the same
//     name already exists. The role was not added.").
//   * Billing/trial grouping defaults (cmdUpdate_Click L212-230: billing/trial applied only when fee+period+frequency
//     are set and frequency != "N"; otherwise defaulted).
//   * System-role guardrails (cannot create/rename/remove the Administrator / Registered Users roles).
public sealed class CreateRoleValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleValidator()
    {
        // MIGRATION: EditRoles.ascx valRoleName RequiredFieldValidator on txtRoleName -> NotEmpty; txtRoleName MaxLength=50.
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("You Must Enter a Valid Name")
            .MaximumLength(50);

        // MIGRATION: EditRoles.ascx valServiceFee2 CompareValidator GreaterThanEqual 0 -> ServiceFee >= 0 (float;
        // default 0 passes). DataTypeCheck (valServiceFee1, Currency) is N/A for a typed float DTO field.
        RuleFor(x => x.ServiceFee)
            .GreaterThanOrEqualTo(0).WithMessage("Service Fee Must Be Greater Than or Equal to Zero");

        // MIGRATION: EditRoles.ascx valBillingPeriod2 CompareValidator GreaterThan 0. The legacy CompareValidator
        // PASSES on an empty textbox; the DTO uses a non-nullable int defaulting to 0 to represent "not supplied"
        // (RoleService defaults it to 1 when billing is unused), so the rule is applied only when a value is present
        // (BillingPeriod != 0) -- preserving pass-on-empty while still rejecting negatives. (DataTypeCheck Integer = N/A.)
        RuleFor(x => x.BillingPeriod)
            .GreaterThan(0).WithMessage("Billing Period Must Be Greater Than Zero")
            .When(x => x.BillingPeriod != 0);

        // MIGRATION: EditRoles.ascx valTrialFee2 CompareValidator GreaterThanEqual 0 -> TrialFee >= 0 (float; default 0 passes).
        RuleFor(x => x.TrialFee)
            .GreaterThanOrEqualTo(0).WithMessage("Trial Fee Must Be Greater Than or Equal to Zero");

        // MIGRATION: EditRoles.ascx valTrialPeriod2 CompareValidator GreaterThan 0 -- same pass-on-empty mapping as BillingPeriod.
        RuleFor(x => x.TrialPeriod)
            .GreaterThan(0).WithMessage("Trial Period Must Be Greater Than Zero")
            .When(x => x.TrialPeriod != 0);
    }
}
