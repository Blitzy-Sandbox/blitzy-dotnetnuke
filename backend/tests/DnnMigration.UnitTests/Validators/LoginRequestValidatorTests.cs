using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for LoginRequestValidator. Asserts rules and verbatim messages migrated
// from legacy DNN login/authentication validation (Website/admin/Security/SendPassword.ascx.vb) and
// Library/Components/Security/PortalSecurity.vb. NOTE: "Username Is Required." and
// "Password Is Required." both HAVE a trailing period. Tests encode parity ONLY.
public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    private const string UsernameRequired = "Username Is Required.";
    private const string PasswordRequired = "Password Is Required.";

    private static LoginRequest Valid() => new()
    {
        Username = "admin",
        Password = "P@ssw0rd",
        PortalId = 0
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

    // ---- Username: NotEmpty (custom message) ----

    [Fact]
    public void Username_empty_fails_with_message()
    {
        var dto = Valid();
        dto.Username = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(UsernameRequired);
    }

    [Fact]
    public void Username_whitespace_fails_with_message()
    {
        var dto = Valid();
        dto.Username = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(UsernameRequired);
    }

    [Fact]
    public void Username_populated_passes()
    {
        var dto = Valid();
        dto.Username = "jdoe";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    // ---- Password: NotEmpty (custom message) ----

    [Fact]
    public void Password_empty_fails_with_message()
    {
        var dto = Valid();
        dto.Password = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(PasswordRequired);
    }

    [Fact]
    public void Password_whitespace_fails_with_message()
    {
        var dto = Valid();
        dto.Password = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(PasswordRequired);
    }

    [Fact]
    public void Password_populated_passes()
    {
        var dto = Valid();
        dto.Password = "secret123";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    // ---- PortalId: GreaterThanOrEqualTo(0) (default message) ----

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

    [Fact]
    public void PortalId_positive_passes()
    {
        var dto = Valid();
        dto.PortalId = 5;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalId);
    }
}
