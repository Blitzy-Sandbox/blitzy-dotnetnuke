using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies CreateTabValidator parity with the legacy Website/admin/Tabs controls
// (e.g. ManageTabs.ascx.vb). The legacy tab/page admin requires a page (tab) name and treats the
// auto-refresh interval as optional and non-negative. Mirrors the two rules declared in
// DnnMigration.Application.Validators.CreateTabValidator:
//   * TabName        -> NotEmpty (required).
//   * RefreshInterval -> GreaterThanOrEqualTo(0) ONLY .When(x => x.RefreshInterval.HasValue),
//                        i.e. a null interval skips the rule (optional), a negative value fails.
// The source-of-truth Library/Components/Tabs/TabInfo.vb carries no DataAnnotation validation
// attributes (RefreshInterval is a backing-field Integer, TabName a backing-field String), so all
// validation semantics originate in the FluentValidation validator under test. Contributes to
// Validation Gate 2 (dotnet test) and must build clean under Gate 1 (--warnaserror).
public class CreateTabValidatorTests
{
    private readonly CreateTabValidator _validator = new();

    // MIGRATION: Minimal fully-valid baseline. Only TabName is required by the validator; leaving
    // RefreshInterval null exercises the When(HasValue) guard that SKIPS the non-negative rule, so
    // this DTO produces zero validation errors.
    private static CreateTabDto ValidDto() => new()
    {
        TabName = "Home"
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: tab/page admin requires a tab name (legacy RequiredFieldValidator on the page-name
    // field). Both null and empty string violate RuleFor(x => x.TabName).NotEmpty().
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TabName_Missing_Fails(string? tabName)
    {
        var model = ValidDto();
        model.TabName = tabName;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TabName);
    }

    [Fact]
    public void TabName_Provided_NoError()
    {
        var model = ValidDto();
        model.TabName = "About Us";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.TabName);
    }

    // MIGRATION: TabInfo.RefreshInterval must be non-negative when supplied. A negative HasValue
    // value violates RuleFor(x => x.RefreshInterval).GreaterThanOrEqualTo(0).When(HasValue).
    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public void RefreshInterval_Negative_Fails(int interval)
    {
        var model = ValidDto();
        model.RefreshInterval = interval;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.RefreshInterval);
    }

    // MIGRATION: a null RefreshInterval is allowed (When-guard skips the rule), matching the legacy
    // optional auto-refresh interval semantics.
    [Fact]
    public void RefreshInterval_Null_NoError()
    {
        var model = ValidDto();
        model.RefreshInterval = null;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.RefreshInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    public void RefreshInterval_NonNegative_NoError(int interval)
    {
        var model = ValidDto();
        model.RefreshInterval = interval;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.RefreshInterval);
    }
}
