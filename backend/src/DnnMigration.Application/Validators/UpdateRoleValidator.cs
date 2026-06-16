using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Role-update validation parity with the RoleController.vb update path. The field-level rules
// below are reproduced from Website/admin/Security/EditRoles.ascx.vb (the legacy "Edit Role" admin user
// control): RoleName is required, fees/periods are numeric, and the billing/trial frequencies come from
// the "Frequency" list (default "N"). RoleInfo.vb (DotNetNuke.Security.Roles) declares no validation
// attributes, so every constraint here originates from that admin control rather than the entity.
// MIGRATION: Duplicate role-name detection (GetRoleByName) is stateful and handled in RoleService.
public class UpdateRoleValidator : AbstractValidator<UpdateRoleDto>
{
    // MIGRATION: allowed frequency codes from the legacy "Frequency" list — N(one)/O(ne-time)/D(aily)/
    // W(eekly)/M(onthly)/Y(early); the legacy combo defaults the selection to "N".
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

        // MIGRATION: ServiceFee/TrialFee/BillingPeriod/TrialPeriod parsed as non-negative numbers.
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BillingPeriod).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialPeriod).GreaterThanOrEqualTo(0);

        // MIGRATION: frequency from the "Frequency" list (N/O/D/W/M/Y, default N); checked only when supplied.
        // The `f is not null &&` guard satisfies nullable-reference analysis so the array .Contains call does
        // not raise CS8604 under the solution's --warnaserror policy.
        RuleFor(x => x.BillingFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f))
            .When(x => !string.IsNullOrEmpty(x.BillingFrequency));

        RuleFor(x => x.TrialFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f))
            .When(x => !string.IsNullOrEmpty(x.TrialFrequency));
    }
}
