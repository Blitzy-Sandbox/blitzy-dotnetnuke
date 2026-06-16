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
        // MIGRATION: EditRoles.ascx.vb RoleName RequiredFieldValidator.
        RuleFor(x => x.RoleName)
            .NotEmpty();

        // MIGRATION: ServiceFee/TrialFee/BillingPeriod/TrialPeriod parsed as non-negative numbers in EditRoles.ascx.vb.
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BillingPeriod).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialPeriod).GreaterThanOrEqualTo(0);

        // MIGRATION: EditRoles.ascx.vb binds Billing/Trial frequency from the "Frequency" list (N/O/D/W/M/Y, default N); validated only when supplied.
        RuleFor(x => x.BillingFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f))
            .When(x => !string.IsNullOrEmpty(x.BillingFrequency));

        RuleFor(x => x.TrialFrequency)
            .Must(f => f is not null && ValidFrequencies.Contains(f))
            .When(x => !string.IsNullOrEmpty(x.TrialFrequency));
    }
}
