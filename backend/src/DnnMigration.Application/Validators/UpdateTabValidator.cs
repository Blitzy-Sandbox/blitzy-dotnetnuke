using FluentValidation;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.Application.Validators;

// MIGRATION: Tab(page)-update validation parity (TabController.vb update path). Rules from
// Website/admin/Tabs/ManageTabs.ascx + App_LocalResources/ManageTabs.ascx.resx (page-name
// RequiredFieldValidator only). TabInfo.vb has no validation attributes.
// MIGRATION: The page-name message reproduces the resourcekey-bound text actually displayed by valTabName
// (resx "valTabName.ErrorMessage" = "Page Name Is Required"; the markup's inline "Tab Name Is Required" is
// overridden by the resource). The "<br>" presentation prefix is dropped for JSON/Angular delivery.
// MIGRATION: RefreshInterval is intentionally NOT range-validated — legacy ManageTabs.ascx.vb (L298-299) only
// stored it "If ...Length > 0 AndAlso IsNumeric(...)" with NO lower-bound check, and txtRefreshInterval has no
// validator. The DTO's int? typing enforces "numeric integer"; nullability preserves the optional/unset case.
// MIGRATION: Reserved device-name, duplicate TabPath, and circular parent-reference checks
// are stateful and handled in TabService, not here.
public class UpdateTabValidator : AbstractValidator<UpdateTabDto>
{
    public UpdateTabValidator()
    {
        // MIGRATION: update targets an existing tab(page) - key must be valid (> 0).
        RuleFor(x => x.TabID)
            .GreaterThan(0).WithMessage("A valid Tab is required");

        // MIGRATION: ManageTabs.ascx valTabName RequiredFieldValidator (resx "Page Name Is Required").
        RuleFor(x => x.TabName)
            .NotEmpty().WithMessage("Page Name Is Required");
    }
}
