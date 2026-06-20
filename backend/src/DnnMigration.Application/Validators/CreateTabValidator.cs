using FluentValidation;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.Application.Validators;

// MIGRATION: Tab(page)-create validation parity. Rules from Website/admin/Tabs/ManageTabs.ascx +
// App_LocalResources/ManageTabs.ascx.resx (page-name RequiredFieldValidator only). TabInfo.vb has no
// validation attributes.
// MIGRATION: The page-name message reproduces the resourcekey-bound text actually displayed by valTabName
// (resx "valTabName.ErrorMessage" = "Page Name Is Required"; the markup's inline "Tab Name Is Required" is
// overridden by the resource). The "<br>" presentation prefix is dropped for JSON/Angular delivery.
// MIGRATION: RefreshInterval is intentionally NOT range-validated — legacy ManageTabs.ascx.vb (L298-299) only
// stored it "If ...Length > 0 AndAlso IsNumeric(...)" with NO lower-bound check, and txtRefreshInterval has no
// validator. The DTO's int? typing enforces "numeric integer"; nullability preserves the optional/unset case.
// MIGRATION: Reserved device-name rejection, duplicate TabPath, and circular parent-reference checks
// are stateful tree/DB checks handled in TabService, not here.
public class CreateTabValidator : AbstractValidator<CreateTabDto>
{
    public CreateTabValidator()
    {
        // MIGRATION: ManageTabs.ascx valTabName RequiredFieldValidator (resx "Page Name Is Required").
        RuleFor(x => x.TabName)
            .NotEmpty().WithMessage("Page Name Is Required");
    }
}
