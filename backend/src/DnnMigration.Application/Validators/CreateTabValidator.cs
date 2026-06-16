using FluentValidation;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.Application.Validators;

// MIGRATION: Tab(page)-create validation parity. Rules from Website/admin/Tabs/ManageTabs.ascx.vb
// (TabName required). TabInfo.vb has no validation attributes.
// MIGRATION: Reserved device-name rejection, duplicate TabPath, and circular parent-reference checks
// are stateful tree/DB checks handled in TabService, not here.
public class CreateTabValidator : AbstractValidator<CreateTabDto>
{
    public CreateTabValidator()
    {
        // MIGRATION: ManageTabs.ascx.vb page-name RequiredFieldValidator.
        RuleFor(x => x.TabName)
            .NotEmpty();

        // MIGRATION: TabInfo.RefreshInterval non-negative when supplied.
        RuleFor(x => x.RefreshInterval)
            .GreaterThanOrEqualTo(0)
            .When(x => x.RefreshInterval.HasValue);
    }
}
