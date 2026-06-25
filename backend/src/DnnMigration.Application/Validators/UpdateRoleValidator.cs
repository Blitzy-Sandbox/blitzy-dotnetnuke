using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Security/EditRoles.ascx + EditRoles.ascx.vb (cmdUpdate_Click). In the
// legacy edit screen RoleName is shown read-only (txtRoleName.Visible=False, valRoleName.Enabled=False at L134) yet
// cmdUpdate_Click still re-persists RoleInfo.RoleName, so it remains required here for behavioral parity (AAP 0.7.1).
// Synchronous-only validator (no repository/data access): the system-role guardrails (cannot rename/remove the
// Administrator / Registered Users roles, EditRoles.ascx.vb L174-178) and billing/trial grouping defaults are
// multi-entity concerns enforced in RoleService, not here.
public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleValidator()
    {
        // MIGRATION: EditRoles.ascx valRoleName RequiredFieldValidator + txtRoleName MaxLength=50 (RoleName is
        // re-persisted on update despite being read-only in the UI).
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("You Must Enter a Valid Name")
            .MaximumLength(50);

        // MIGRATION: EditRoles.ascx valServiceFee2 CompareValidator GreaterThanEqual 0 -> ServiceFee >= 0 (float; default 0 passes).
        RuleFor(x => x.ServiceFee)
            .GreaterThanOrEqualTo(0).WithMessage("Service Fee Must Be Greater Than or Equal to Zero");

        // MIGRATION: EditRoles.ascx valBillingPeriod2 CompareValidator GreaterThan 0. CompareValidator passes on
        // empty; DTO int defaults to 0 ("not supplied", RoleService defaults to 1), so applied only when non-zero --
        // preserving pass-on-empty while still rejecting negatives.
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
