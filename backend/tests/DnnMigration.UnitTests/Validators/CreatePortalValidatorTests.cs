using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for CreatePortalValidator. Asserts the exact rules and verbatim
// error messages migrated from legacy DNN portal-creation validation
// (Website/admin/Portal/Signup.ascx.vb, SiteSettings.ascx.vb) and PortalInfo.vb field
// constraints. Tests encode behavioral parity ONLY - they introduce no new rules.
public sealed class CreatePortalValidatorTests
{
    private readonly CreatePortalValidator _validator = new();

    private static CreatePortalRequest Valid() => new()
    {
        PortalName = "Contoso Portal",
        Email = "admin@contoso.com",
        Description = "A valid portal description",
        KeyWords = "cms, portal, dnn",
        HomeDirectory = "Portals/0"
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

    // ---- PortalName: NotEmpty (custom message) + MaximumLength(128) ----

    [Fact]
    public void PortalName_empty_fails_with_required_message()
    {
        var dto = Valid();
        dto.PortalName = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PortalName)
            .WithErrorMessage("Portal Name Is Required.");
    }

    [Fact]
    public void PortalName_whitespace_fails_with_required_message()
    {
        var dto = Valid();
        dto.PortalName = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PortalName)
            .WithErrorMessage("Portal Name Is Required.");
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

    // ---- Email: NotEmpty (custom message) + MaximumLength(100), NO format regex ----

    [Fact]
    public void Email_empty_fails_with_required_message()
    {
        var dto = Valid();
        dto.Email = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email Is Required.");
    }

    [Fact]
    public void Email_null_fails_with_required_message()
    {
        var dto = Valid();
        dto.Email = null;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email Is Required.");
    }

    [Fact]
    public void Email_whitespace_fails_with_required_message()
    {
        var dto = Valid();
        dto.Email = "  ";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email Is Required.");
    }

    [Fact]
    public void Email_exceeding_100_fails()
    {
        var dto = Valid();
        dto.Email = new string('a', 101);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_at_100_passes()
    {
        var dto = Valid();
        dto.Email = new string('a', 100);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // MIGRATION: CreatePortal Email has NO format regex in legacy parity - a malformed
    // address must NOT raise a validation error (only NotEmpty + MaximumLength(100) apply).
    [Fact]
    public void Email_with_invalid_format_passes_no_regex()
    {
        var dto = Valid();
        dto.Email = "not-an-email";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // ---- Description: MaximumLength(500), passes on null ----

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

    // ---- KeyWords: MaximumLength(500), passes on null ----

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

    // ---- HomeDirectory: MaximumLength(100), passes on null ----

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
}
