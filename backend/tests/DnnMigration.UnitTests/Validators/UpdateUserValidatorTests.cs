using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for UpdateUserValidator. Asserts rules and the verbatim InvalidEmail
// message (DOUBLE SPACE between sentences) migrated from legacy DNN user-edit validation
// (Website/admin/Users/User.ascx.vb) and UserInfo.vb. There are NO rules on Username, Password,
// IsApproved, or LockedOut on update. Tests encode behavioral parity ONLY.
public sealed class UpdateUserValidatorTests
{
    private readonly UpdateUserValidator _validator = new();

    // NOTE: DOUBLE SPACE after the period - do NOT collapse.
    private const string InvalidEmail =
        "The email address specified is invalid.  Please specify a valid email address.";

    private static UpdateUserRequest Valid() => new()
    {
        Email = "jdoe@contoso.com",
        DisplayName = "John Doe",
        FirstName = "John",
        LastName = "Doe",
        IsApproved = true,
        LockedOut = false
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

    // ---- Email: NotEmpty + MaximumLength(256) + Matches (custom double-space message) ----

    [Fact]
    public void Email_null_fails()
    {
        var dto = Valid();
        dto.Email = null;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_empty_fails()
    {
        var dto = Valid();
        dto.Email = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_invalid_format_fails_with_invalid_email_message()
    {
        var dto = Valid();
        dto.Email = "not-an-email";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage(InvalidEmail);
    }

    [Fact]
    public void Email_exceeding_256_fails()
    {
        var dto = Valid();
        dto.Email = new string('a', 245) + "@example.com"; // 257 chars, valid format
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_at_256_with_valid_format_passes()
    {
        var dto = Valid();
        dto.Email = new string('a', 244) + "@example.com"; // 256 chars, valid format
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // ---- DisplayName: NotEmpty + MaximumLength(128) ----

    [Fact]
    public void DisplayName_empty_fails()
    {
        var dto = Valid();
        dto.DisplayName = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_null_fails()
    {
        var dto = Valid();
        dto.DisplayName = null;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_exceeding_128_fails()
    {
        var dto = Valid();
        dto.DisplayName = new string('a', 129);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_at_128_passes()
    {
        var dto = Valid();
        dto.DisplayName = new string('a', 128);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    // ---- FirstName: NotEmpty + MaximumLength(50) ----

    [Fact]
    public void FirstName_empty_fails()
    {
        var dto = Valid();
        dto.FirstName = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void FirstName_null_fails()
    {
        var dto = Valid();
        dto.FirstName = null;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void FirstName_exceeding_50_fails()
    {
        var dto = Valid();
        dto.FirstName = new string('a', 51);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void FirstName_at_50_passes()
    {
        var dto = Valid();
        dto.FirstName = new string('a', 50);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
    }

    // ---- LastName: NotEmpty + MaximumLength(50) ----

    [Fact]
    public void LastName_empty_fails()
    {
        var dto = Valid();
        dto.LastName = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void LastName_null_fails()
    {
        var dto = Valid();
        dto.LastName = null;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void LastName_exceeding_50_fails()
    {
        var dto = Valid();
        dto.LastName = new string('a', 51);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void LastName_at_50_passes()
    {
        var dto = Valid();
        dto.LastName = new string('a', 50);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.LastName);
    }

    // ---- IsApproved / LockedOut: NO validation rules (parity) ----

    // MIGRATION: Update has no rule on IsApproved/LockedOut - any combination is valid.
    [Fact]
    public void IsApproved_and_LockedOut_any_value_passes()
    {
        var dto = Valid();
        dto.IsApproved = false;
        dto.LockedOut = true;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.IsApproved);
        result.ShouldNotHaveValidationErrorFor(x => x.LockedOut);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
