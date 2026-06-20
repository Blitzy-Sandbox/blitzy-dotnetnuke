using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies CreateUserValidator parity with UserInfo.vb DataAnnotations (verbatim):
//   Username Required(True); DisplayName Required(True)+MaxLength(128);
//   Email Required(True)+MaxLength(256)+RegularExpressionValidator(glbEmailRegEx);
//   FirstName Required(True)+MaxLength(50); LastName Required(True)+MaxLength(50).
// Password NotEmpty is a create-time credential requirement (no legacy attribute; auth modernization).
public class CreateUserValidatorTests
{
    private readonly CreateUserValidator _validator = new();

    private static CreateUserDto ValidDto() => new()
    {
        Username = "jdoe",
        Password = "P@ssw0rd!",
        DisplayName = "John Doe",
        Email = "john@example.com",
        FirstName = "John",
        LastName = "Doe"
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: UserInfo.Username Required(True).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Username_Missing_Fails(string? username)
    {
        var model = ValidDto();
        model.Username = username;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Username_Provided_NoError()
    {
        var model = ValidDto();
        model.Username = "asmith";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    // MIGRATION: a credential is required when creating a user.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Password_Missing_Fails(string? password)
    {
        var model = ValidDto();
        model.Password = password;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_Provided_NoError()
    {
        var model = ValidDto();
        model.Password = "Str0ng#Pass";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    // MIGRATION: UserInfo.DisplayName Required(True).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DisplayName_Missing_Fails(string? displayName)
    {
        var model = ValidDto();
        model.DisplayName = displayName;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    // MIGRATION: UserInfo.DisplayName MaxLength(128).
    [Fact]
    public void DisplayName_TooLong_Fails()
    {
        var model = ValidDto();
        model.DisplayName = new string('a', 129);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_AtMaxLength_NoError()
    {
        var model = ValidDto();
        model.DisplayName = new string('a', 128);
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    // MIGRATION: UserInfo.Email Required(True).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Email_Missing_Fails(string? email)
    {
        var model = ValidDto();
        model.Email = email;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // MIGRATION: UserInfo.Email RegularExpressionValidator(glbEmailRegEx).
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("plaintext.com")]
    public void Email_InvalidFormat_Fails(string email)
    {
        var model = ValidDto();
        model.Email = email;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // MIGRATION: UserInfo.Email MaxLength(256). 258-char single-@ address: length rule trips.
    [Fact]
    public void Email_TooLong_Fails()
    {
        var model = ValidDto();
        model.Email = new string('a', 252) + "@x.com";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_Valid_NoError()
    {
        var model = ValidDto();
        model.Email = "valid.user@example.org";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // MIGRATION: UserInfo.FirstName Required(True).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FirstName_Missing_Fails(string? firstName)
    {
        var model = ValidDto();
        model.FirstName = firstName;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    // MIGRATION: UserInfo.FirstName MaxLength(50).
    [Fact]
    public void FirstName_TooLong_Fails()
    {
        var model = ValidDto();
        model.FirstName = new string('a', 51);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void FirstName_AtMaxLength_NoError()
    {
        var model = ValidDto();
        model.FirstName = new string('a', 50);
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
    }

    // MIGRATION: UserInfo.LastName Required(True).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LastName_Missing_Fails(string? lastName)
    {
        var model = ValidDto();
        model.LastName = lastName;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    // MIGRATION: UserInfo.LastName MaxLength(50).
    [Fact]
    public void LastName_TooLong_Fails()
    {
        var model = ValidDto();
        model.LastName = new string('a', 51);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void LastName_AtMaxLength_NoError()
    {
        var model = ValidDto();
        model.LastName = new string('a', 50);
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.LastName);
    }
}
