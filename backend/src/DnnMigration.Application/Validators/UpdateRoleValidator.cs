using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Role-update validation parity with the RoleController.vb update path. Field rules are sourced
// from Website/admin/Security/EditRoles.ascx.vb (RoleName RequiredFieldValidator + cmdUpdate_Click numeric/
// frequency parsing); RoleInfo.vb declares no validation attributes, so these rules reproduce the legacy
// ASCX validators rather than entity-level metadata.
// MIGRATION: Duplicate role-name detection (GetRoleByName) is stateful and handled in RoleService.
public class UpdateRoleValidator : AbstractValidator<UpdateRoleDto>
{
    private static readonly string[] ValidFrequencies = { "N", "O", "D", "W", "M", "Y" };

    public UpdateRoleValidator()
    {
        // MIGRATION: update targets an existing role — key must be valid (> 0). System roles
        // (Administrators/Registered Users) guardrails enforced in RoleService.
        RuleFor(x => x.RoleID)
            .GreaterThan(0);

        // MIGRATION: EditRoles.ascx.vb RoleName RequiredFieldValidator.
        RuleFor(x => x.RoleName)
            .NotEmpty();

        // MIGRATION: ServiceFee/TrialFee/BillingPeriod/TrialPeriod parsed as non-negative numbers in EditRoles.ascx.vb.
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BillingPeriod).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialPeriod).GreaterThanOrEqualTo(0);

        // MIGRATION: frequency from the "Frequency" list (N/O/D/W/M/Y, default N); checked only when supplied.
        RuleFor(x => x.BillingFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f))
            .When(x => !string.IsNullOrEmpty(x.BillingFrequency));

        RuleFor(x => x.TrialFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f))
            .When(x => !string.IsNullOrEmpty(x.TrialFrequency));
    }
}
