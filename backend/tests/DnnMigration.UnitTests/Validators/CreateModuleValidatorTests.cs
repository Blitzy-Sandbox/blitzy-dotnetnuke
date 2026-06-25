using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for CreateModuleValidator. Asserts rules and the verbatim Border message
// migrated from legacy DNN module-settings validation (Website/admin/Modules/ModuleSettings.ascx.vb)
// and ModuleInfo.vb. PARITY QUIRK: Border is validated by Matches(@"^[0-9]$") guarded by
// .When(!string.IsNullOrEmpty(Border)) with NO MaximumLength - empty/null Border PASSES, a single
// digit 0-9 PASSES, and anything else FAILS with the one message. Tests encode parity ONLY.
public sealed class CreateModuleValidatorTests
{
    private readonly CreateModuleValidator _validator = new();

    private const string InvalidBorder = "Invalid Border (must be a number between 0 and 9)";

    private static CreateModuleRequest Valid() => new()
    {
        PortalId = 0,
        TabId = 0
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

    // ---- PortalId: GreaterThanOrEqualTo(0) ----

    [Fact]
    public void PortalId_negative_fails()
    {
        var dto = Valid();
        dto.PortalId = -1;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PortalId);
    }

    [Fact]
    public void PortalId_zero_passes()
    {
        var dto = Valid();
        dto.PortalId = 0;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalId);
    }

    // ---- TabId: GreaterThanOrEqualTo(0) ----

    [Fact]
    public void TabId_negative_fails()
    {
        var dto = Valid();
        dto.TabId = -1;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TabId);
    }

    [Fact]
    public void TabId_zero_passes()
    {
        var dto = Valid();
        dto.TabId = 0;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.TabId);
    }

    // ---- Border: Matches(@"^[0-9]$") (custom) .When(!IsNullOrEmpty), NO MaximumLength ----

    // MIGRATION: empty/null Border skips the rule (IsNullOrEmpty guard) - PASSES.
    [Fact]
    public void Border_null_passes()
    {
        var dto = Valid();
        dto.Border = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Border);
    }

    [Fact]
    public void Border_empty_passes()
    {
        var dto = Valid();
        dto.Border = "";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Border);
    }

    [Fact]
    public void Border_single_digit_zero_passes()
    {
        var dto = Valid();
        dto.Border = "0";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Border);
    }

    [Fact]
    public void Border_single_digit_five_passes()
    {
        var dto = Valid();
        dto.Border = "5";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Border);
    }

    [Fact]
    public void Border_single_digit_nine_passes()
    {
        var dto = Valid();
        dto.Border = "9";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Border);
    }

    // MIGRATION: a single space is NOT empty (IsNullOrEmpty, not IsNullOrWhiteSpace),
    // so the rule runs and fails.
    [Fact]
    public void Border_whitespace_fails_with_message()
    {
        var dto = Valid();
        dto.Border = " ";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Border)
            .WithErrorMessage(InvalidBorder);
    }

    [Fact]
    public void Border_multiple_digits_fails_with_message()
    {
        var dto = Valid();
        dto.Border = "55";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Border)
            .WithErrorMessage(InvalidBorder);
    }

    [Fact]
    public void Border_non_digit_fails_with_message()
    {
        var dto = Valid();
        dto.Border = "a";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Border)
            .WithErrorMessage(InvalidBorder);
    }
}
