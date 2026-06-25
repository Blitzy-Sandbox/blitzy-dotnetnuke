using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for CreateTabValidator. Asserts rules and the verbatim TabName message
// migrated from legacy DNN page/tab validation (Website/admin/Tabs/ManageTabs.ascx.vb), TabInfo.vb,
// and TabController.vb. NOTE: "Tab Name Is Required" has NO trailing period. Tests encode parity ONLY.
public sealed class CreateTabValidatorTests
{
    private readonly CreateTabValidator _validator = new();

    private const string TabNameRequired = "Tab Name Is Required";

    private static CreateTabRequest Valid() => new()
    {
        TabName = "Home"
    };

    [Fact]
    public void Valid_request_passes_with_no_errors()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_request_reports_IsValid_true()
    {
        var result = _validator.Validate(Valid());
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ---- TabName: NotEmpty (custom message) + MaximumLength(50) ----

    [Fact]
    public void TabName_null_fails_with_message()
    {
        var dto = Valid();
        dto.TabName = null;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TabName)
            .WithErrorMessage(TabNameRequired);
    }

    [Fact]
    public void TabName_empty_fails_with_message()
    {
        var dto = Valid();
        dto.TabName = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TabName)
            .WithErrorMessage(TabNameRequired);
    }

    [Fact]
    public void TabName_whitespace_fails_with_message()
    {
        var dto = Valid();
        dto.TabName = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TabName)
            .WithErrorMessage(TabNameRequired);
    }

    [Fact]
    public void TabName_exceeding_50_fails()
    {
        var dto = Valid();
        dto.TabName = new string('a', 51);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TabName);
    }

    [Fact]
    public void TabName_at_50_passes()
    {
        var dto = Valid();
        dto.TabName = new string('a', 50);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.TabName);
    }

    // ---- Title: MaximumLength(200) ----

    [Fact]
    public void Title_null_passes()
    {
        var dto = Valid();
        dto.Title = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_exceeding_200_fails()
    {
        var dto = Valid();
        dto.Title = new string('a', 201);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_at_200_passes()
    {
        var dto = Valid();
        dto.Title = new string('a', 200);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    // ---- Description: MaximumLength(500) ----

    [Fact]
    public void Description_null_passes()
    {
        var dto = Valid();
        dto.Description = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_exceeding_500_fails()
    {
        var dto = Valid();
        dto.Description = new string('a', 501);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_at_500_passes()
    {
        var dto = Valid();
        dto.Description = new string('a', 500);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    // ---- KeyWords: MaximumLength(500) ----

    [Fact]
    public void KeyWords_null_passes()
    {
        var dto = Valid();
        dto.KeyWords = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.KeyWords);
    }

    [Fact]
    public void KeyWords_exceeding_500_fails()
    {
        var dto = Valid();
        dto.KeyWords = new string('a', 501);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.KeyWords);
    }

    [Fact]
    public void KeyWords_at_500_passes()
    {
        var dto = Valid();
        dto.KeyWords = new string('a', 500);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.KeyWords);
    }

    // ---- PageHeadText: MaximumLength(500) ----

    [Fact]
    public void PageHeadText_null_passes()
    {
        var dto = Valid();
        dto.PageHeadText = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PageHeadText);
    }

    [Fact]
    public void PageHeadText_exceeding_500_fails()
    {
        var dto = Valid();
        dto.PageHeadText = new string('a', 501);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PageHeadText);
    }

    [Fact]
    public void PageHeadText_at_500_passes()
    {
        var dto = Valid();
        dto.PageHeadText = new string('a', 500);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PageHeadText);
    }
}
