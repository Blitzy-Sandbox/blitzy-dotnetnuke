using FluentValidation;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Validators;

// MIGRATION: Role-create validation parity. Rules from Website/admin/Security/EditRoles.ascx.vb
// (RoleName required; fee/period numeric; frequency list default "N"). RoleInfo.vb has no validation attributes.
// MIGRATION: Duplicate role-name detection (GetRoleByName) is stateful and handled in RoleService.
public class CreateRoleValidator : AbstractValidator<CreateRoleDto>
{
    // MIGRATION: EditRoles.ascx.vb "Frequency" list values (N=None, O=one-time, D=day, W=week, M=month, Y=year).
    private static readonly string[] ValidFrequencies = { "N", "O", "D", "W", "M", "Y" };

    public CreateRoleValidator()
    {
        // MIGRATION: EditRoles.ascx.vb RoleName RequiredFieldValidator (message "You Must Enter a Valid Name").
        // MaxLength(50) matches Roles.RoleName nvarchar(50) (DotNetNuke.Schema.SqlDataProvider).
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("You Must Enter a Valid Name")
            .MaximumLength(50).WithMessage("Role Name must be 50 characters or fewer.");

        // MIGRATION: Roles.Description nvarchar(1000) — length enforced for schema fidelity (optional field; MaximumLength is null-safe).
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.");

        // MIGRATION: ServiceFee/TrialFee/BillingPeriod/TrialPeriod parsed as non-negative numbers in EditRoles.ascx.vb.
        // Legacy "Trial ... Must Be Greater Than Zero" is adapted to ">= Zero" to match the GreaterThanOrEqualTo(0) rule.
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0).WithMessage("Service Fee Must Be Greater Than or Equal to Zero");
        RuleFor(x => x.TrialFee).GreaterThanOrEqualTo(0).WithMessage("Trial Fee Must Be Greater Than or Equal to Zero");
        RuleFor(x => x.BillingPeriod).GreaterThanOrEqualTo(0).WithMessage("Billing Period Must Be Greater Than or Equal to Zero");
        RuleFor(x => x.TrialPeriod).GreaterThanOrEqualTo(0).WithMessage("Trial Period Must Be Greater Than or Equal to Zero");

        // MIGRATION: EditRoles.ascx.vb binds Billing/Trial frequency from the "Frequency" list (N/O/D/W/M/Y, default N);
        // validated only when supplied. MaximumLength(1) matches Roles.BillingFrequency/TrialFrequency char(1).
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
