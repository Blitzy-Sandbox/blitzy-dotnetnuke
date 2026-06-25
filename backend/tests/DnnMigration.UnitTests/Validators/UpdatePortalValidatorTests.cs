using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for UpdatePortalValidator. Asserts rules migrated from legacy DNN
// portal-settings update validation (Website/admin/Portal/SiteSettings.ascx.vb) and
// PortalInfo.vb field constraints. PARITY QUIRK: PortalName has NO NotEmpty rule on update
// (only MaximumLength(128)); an empty PortalName must PASS. Tests encode parity ONLY.
public sealed class UpdatePortalValidatorTests
{
    private readonly UpdatePortalValidator _validator = new();

    private static UpdatePortalRequest Valid() => new()
    {
        PortalId = 1,
        PortalName = "Contoso Portal",
        Description = "A valid portal description",
        KeyWords = "cms, portal, dnn",
        FooterText = "Copyright Contoso",
        HomeDirectory = "Portals/0",
        ProcessorUserId = "processor-user",
        ProcessorPassword = "processor-pass"
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

    // ---- PortalName: MaximumLength(128) ONLY - NO NotEmpty (parity quirk) ----

    // MIGRATION: On update, PortalName has NO NotEmpty rule (unlike create). An empty value
    // must PASS - only the 128-char maximum applies.
    [Fact]
    public void PortalName_empty_passes_no_notempty_rule()
    {
        var dto = Valid();
        dto.PortalName = "";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalName);
    }

    [Fact]
    public void PortalName_whitespace_passes_no_notempty_rule()
    {
        var dto = Valid();
        dto.PortalName = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalName);
    }

    [Fact]
    public void PortalName_exceeding_128_fails()
    {
        var dto = Valid();
        dto.PortalName = new string('a', 129);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PortalName);
    }

    [Fact]
    public void PortalName_at_128_passes()
    {
        var dto = Valid();
        dto.PortalName = new string('a', 128);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalName);
    }

    // ---- Description: MaximumLength(475) ----

    [Fact]
    public void Description_null_passes()
    {
        var dto = Valid();
        dto.Description = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_exceeding_475_fails()
    {
        var dto = Valid();
        dto.Description = new string('a', 476);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_at_475_passes()
    {
        var dto = Valid();
        dto.Description = new string('a', 475);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    // ---- KeyWords: MaximumLength(475) ----

    [Fact]
    public void KeyWords_null_passes()
    {
        var dto = Valid();
        dto.KeyWords = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.KeyWords);
    }

    [Fact]
    public void KeyWords_exceeding_475_fails()
    {
        var dto = Valid();
        dto.KeyWords = new string('a', 476);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.KeyWords);
    }

    [Fact]
    public void KeyWords_at_475_passes()
    {
        var dto = Valid();
        dto.KeyWords = new string('a', 475);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.KeyWords);
    }

    // ---- FooterText: MaximumLength(100) ----

    [Fact]
    public void FooterText_null_passes()
    {
        var dto = Valid();
        dto.FooterText = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.FooterText);
    }

    [Fact]
    public void FooterText_exceeding_100_fails()
    {
        var dto = Valid();
        dto.FooterText = new string('a', 101);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.FooterText);
    }

    [Fact]
    public void FooterText_at_100_passes()
    {
        var dto = Valid();
        dto.FooterText = new string('a', 100);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.FooterText);
    }

    // ---- HomeDirectory: MaximumLength(100) ----

    [Fact]
    public void HomeDirectory_null_passes()
    {
        var dto = Valid();
        dto.HomeDirectory = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.HomeDirectory);
    }

    [Fact]
    public void HomeDirectory_exceeding_100_fails()
    {
        var dto = Valid();
        dto.HomeDirectory = new string('a', 101);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.HomeDirectory);
    }

    [Fact]
    public void HomeDirectory_at_100_passes()
    {
        var dto = Valid();
        dto.HomeDirectory = new string('a', 100);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.HomeDirectory);
    }

    // ---- ProcessorUserId: MaximumLength(50) ----

    [Fact]
    public void ProcessorUserId_null_passes()
    {
        var dto = Valid();
        dto.ProcessorUserId = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ProcessorUserId);
    }

    [Fact]
    public void ProcessorUserId_exceeding_50_fails()
    {
        var dto = Valid();
        dto.ProcessorUserId = new string('a', 51);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ProcessorUserId);
    }

    [Fact]
    public void ProcessorUserId_at_50_passes()
    {
        var dto = Valid();
        dto.ProcessorUserId = new string('a', 50);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ProcessorUserId);
    }

    // ---- ProcessorPassword: MaximumLength(50) ----

    [Fact]
    public void ProcessorPassword_null_passes()
    {
        var dto = Valid();
        dto.ProcessorPassword = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ProcessorPassword);
    }

    [Fact]
    public void ProcessorPassword_exceeding_50_fails()
    {
        var dto = Valid();
        dto.ProcessorPassword = new string('a', 51);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ProcessorPassword);
    }

    [Fact]
    public void ProcessorPassword_at_50_passes()
    {
        var dto = Valid();
        dto.ProcessorPassword = new string('a', 50);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ProcessorPassword);
    }
}
