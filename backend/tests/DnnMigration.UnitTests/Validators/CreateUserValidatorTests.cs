using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for CreateUserValidator. Asserts rules and VERBATIM error messages
// (including DOUBLE SPACES between sentences) migrated from legacy DNN user-creation validation
// (Website/admin/Users/User.ascx.vb, ManageUsers.ascx.vb) and UserInfo.vb constraints. The
// password + confirmation rules are conditional: they run ONLY when Password is non-empty.
// Tests encode behavioral parity ONLY - they introduce no new rules.
public sealed class CreateUserValidatorTests
{
    private readonly CreateUserValidator _validator = new();

    // NOTE: the following messages contain DOUBLE SPACES after each period - do NOT collapse.
    private const string InvalidUsername =
        "The username specified is invalid.  Please specify a valid username.";
    private const string InvalidEmail =
        "The email address specified is invalid.  Please specify a valid email address.";
    private const string InvalidPassword =
        "The password specified is invalid.  Please specify a valid password.  Passwords must be at least 7 characters in length and contain at least 0 non-alphanumeric characters.";
    private const string PasswordMismatch =
        "The Password and Confirmation Passwords do not match";

    private static CreateUserRequest Valid() => new()
    {
        PortalId = 0,
        Username = "jdoe",
        Email = "jdoe@contoso.com",
        DisplayName = "John Doe",
        FirstName = "John",
        LastName = "Doe",
        Password = "password1",
        Confirm = "password1"
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

    // ---- Username: NotEmpty (custom, double-space message); no MaximumLength ----

    [Fact]
    public void Username_empty_fails_with_invalid_username_message()
    {
        var dto = Valid();
        dto.Username = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(InvalidUsername);
    }

    [Fact]
    public void Username_whitespace_fails_with_invalid_username_message()
    {
        var dto = Valid();
        dto.Username = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(InvalidUsername);
    }

    // ---- DisplayName: NotEmpty + MaximumLength(128) (default messages) ----

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

    // ---- Password block: runs ONLY When(!string.IsNullOrEmpty(Password)) ----

    // MIGRATION: When Password is null the entire password + confirm block is skipped.
    [Fact]
    public void Password_null_skips_block_and_passes()
    {
        var dto = Valid();
        dto.Password = null;
        dto.Confirm = "ignored-because-block-skipped";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
        result.ShouldNotHaveValidationErrorFor(x => x.Confirm);
    }

    // MIGRATION: An empty Password also skips the block (IsNullOrEmpty).
    [Fact]
    public void Password_empty_skips_block_and_passes()
    {
        var dto = Valid();
        dto.Password = "";
        dto.Confirm = "ignored-because-block-skipped";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
        result.ShouldNotHaveValidationErrorFor(x => x.Confirm);
    }

    [Fact]
    public void Password_shorter_than_7_fails_with_invalid_password_message()
    {
        var dto = Valid();
        dto.Password = "abc";
        dto.Confirm = "abc"; // keep Confirm equal so only the length rule fails
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(InvalidPassword);
    }

    [Fact]
    public void Password_at_7_passes()
    {
        var dto = Valid();
        dto.Password = "1234567";
        dto.Confirm = "1234567";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_exceeding_20_fails()
    {
        var dto = Valid();
        dto.Password = new string('a', 21);
        dto.Confirm = new string('a', 21);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_at_20_passes()
    {
        var dto = Valid();
        dto.Password = new string('a', 20);
        dto.Confirm = new string('a', 20);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    // ---- Confirm: Equal(Password) (custom message), inside the When block ----

    [Fact]
    public void Confirm_not_matching_password_fails_with_mismatch_message()
    {
        var dto = Valid();
        dto.Password = "password1";
        dto.Confirm = "different";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Confirm)
            .WithErrorMessage(PasswordMismatch);
    }

    [Fact]
    public void Confirm_matching_password_passes()
    {
        var dto = Valid();
        dto.Password = "password1";
        dto.Confirm = "password1";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Confirm);
    }
}
