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
            .GreaterThan(0).WithMessage("A valid role identifier is required.");

        // MIGRATION: EditRoles.ascx.vb RoleName RequiredFieldValidator (message "You Must Enter a Valid Name").
        // MaxLength(50) matches Roles.RoleName nvarchar(50) (DotNetNuke.Schema.SqlDataProvider).
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("You Must Enter a Valid Name")
            .MaximumLength(50).WithMessage("Role Name must be 50 characters or fewer.");

        // MIGRATION: Roles.Description nvarchar(1000) — length enforced for schema fidelity (optional field; MaximumLength is null-safe).
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.");

        // MIGRATION: ServiceFee/TrialFee/BillingPeriod/TrialPeriod parsed as non-negative numbers.
        // Legacy "Trial ... Must Be Greater Than Zero" is adapted to ">= Zero" to match the GreaterThanOrEqualTo(0) rule.
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0).WithMessage("Service Fee Must Be Greater Than or Equal to Zero");
        RuleFor(x => x.TrialFee).GreaterThanOrEqualTo(0).WithMessage("Trial Fee Must Be Greater Than or Equal to Zero");
        RuleFor(x => x.BillingPeriod).GreaterThanOrEqualTo(0).WithMessage("Billing Period Must Be Greater Than or Equal to Zero");
        RuleFor(x => x.TrialPeriod).GreaterThanOrEqualTo(0).WithMessage("Trial Period Must Be Greater Than or Equal to Zero");

        // MIGRATION: frequency from the "Frequency" list (N/O/D/W/M/Y, default N); checked only when supplied.
        // The `f is not null &&` guard satisfies nullable-reference analysis so the array .Contains call does
        // not raise CS8604 under the solution's --warnaserror policy. MaximumLength(1) matches char(1).
        RuleFor(x => x.BillingFrequency)
            .MaximumLength(1).WithMessage("Billing Frequency must be a single character.")
            .Must(f => f is not null && ValidFrequencies.Contains(f)).WithMessage("Billing Frequency is not valid.")
            .When(x => !string.IsNullOrEmpty(x.BillingFrequency));

        RuleFor(x => x.TrialFrequency)
            .MaximumLength(1).WithMessage("Trial Frequency must be a single character.")
            .Must(f => f is not null && ValidFrequencies.Contains(f)).WithMessage("Trial Frequency is not valid.")
            .When(x => !string.IsNullOrEmpty(x.TrialFrequency));

        // MIGRATION: Roles.RSVPCode nvarchar(50) — length enforced for schema fidelity (optional field; MaximumLength is null-safe).
        RuleFor(x => x.RSVPCode)
            .MaximumLength(50).WithMessage("RSVP Code must be 50 characters or fewer.");

        // MIGRATION: Roles.IconFile nvarchar(100) — length enforced for schema fidelity (optional field; MaximumLength is null-safe).
        RuleFor(x => x.IconFile)
            .MaximumLength(100).WithMessage("Icon File must be 100 characters or fewer.");
    }
}
