using DnnMigration.Application.DTOs;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="CreateUserDtoValidator"/>, the FluentValidation
/// validator that guards <c>POST /api/users</c>.
/// </summary>
/// <remarks>
/// MIGRATION: these tests pin the behavioural parity of the migrated "Add User"
/// validation to the legacy DotNetNuke Web Forms screen
/// (<c>Website/admin/Users/User.ascx(.vb)</c>), the identity-field attributes on
/// <c>Library/Components/Users/UserInfo.vb</c> (Required / MaxLength / e-mail
/// RegularExpressionValidator), and the password policy in
/// <c>Library/Components/Users/UserController.vb</c> <c>ValidatePassword</c>
/// (lines 1067-1091).
///
/// The single highest-value parity check is the <c>RandomPassword</c> bypass: the
/// legacy screen skipped ALL password validation when the "generate random
/// password" checkbox (<c>chkRandom</c>) was set, because the server generated the
/// password itself. That behaviour maps to the validator gating every password
/// rule behind <c>When(x =&gt; !x.RandomPassword)</c>, and is asserted directly by
/// <see cref="RandomPassword_true_bypasses_all_password_rules"/>.
///
/// Assertions use <c>FluentValidation.TestHelper</c> (<c>TestValidate</c> +
/// <c>ShouldHaveValidationErrorFor</c> / <c>ShouldNotHaveValidationErrorFor</c> /
/// <c>ShouldNotHaveAnyValidationErrors</c>) with <c>FluentAssertions</c> for the
/// result-level checks. Error messages that reproduce the legacy UI text are
/// matched verbatim.
/// </remarks>
public class UserValidatorTests
{
    /// <summary>The system under test. It is stateless, so a single shared instance is safe.</summary>
    private readonly CreateUserDtoValidator _sut = new();

    /// <summary>
    /// Builds a fully valid <see cref="CreateUserDto"/> baseline that every test mutates
    /// (via record <c>with</c> expressions) to isolate a single rule.
    /// </summary>
    /// <remarks>
    /// The baseline password is 8 characters (>= the 7-character minimum) and purely
    /// alphanumeric, which is valid because the migrated policy requires
    /// <c>minRequiredNonalphanumericCharacters = 0</c> (Website/development.config).
    /// <c>RandomPassword</c> is false so the password rules are in force, and
    /// <c>DisplayName</c> is intentionally omitted (null) to exercise the optional path.
    /// </remarks>
    private static CreateUserDto ValidCreate() => new()
    {
        Username = "jdoe",
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        Password = "Passw0rd",
        ConfirmPassword = "Passw0rd",
        RandomPassword = false,
        PortalID = 0,
    };

    // ---------------------------------------------------------------------
    // Phase 2 - the valid baseline passes cleanly.
    // ---------------------------------------------------------------------

    [Fact]
    public void Valid_model_has_no_validation_errors()
    {
        var result = _sut.TestValidate(ValidCreate());

        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------
    // Phase 3 - identity field rules (Username / FirstName / LastName / Email).
    // MIGRATION: UserInfo.vb marks these Required; FirstName/LastName MaxLength(50),
    // Email MaxLength(256) + e-mail format.
    // ---------------------------------------------------------------------

    [Fact]
    public void Username_empty_is_error()
    {
        var dto = ValidCreate() with { Username = string.Empty };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void FirstName_empty_is_error()
    {
        var dto = ValidCreate() with { FirstName = string.Empty };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData(50, false)] // exactly the MaximumLength(50) cap -> allowed
    [InlineData(51, true)]  // one over the cap -> rejected
    public void FirstName_length_boundary_is_enforced(int length, bool expectError)
    {
        var dto = ValidCreate() with { FirstName = new string('a', length) };

        var result = _sut.TestValidate(dto);

        if (expectError)
        {
            result.ShouldHaveValidationErrorFor(x => x.FirstName);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
        }
    }

    [Fact]
    public void LastName_empty_is_error()
    {
        var dto = ValidCreate() with { LastName = string.Empty };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData(50, false)] // exactly the MaximumLength(50) cap -> allowed
    [InlineData(51, true)]  // one over the cap -> rejected
    public void LastName_length_boundary_is_enforced(int length, bool expectError)
    {
        var dto = ValidCreate() with { LastName = new string('a', length) };

        var result = _sut.TestValidate(dto);

        if (expectError)
        {
            result.ShouldHaveValidationErrorFor(x => x.LastName);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.LastName);
        }
    }

    [Fact]
    public void Email_empty_is_error()
    {
        var dto = ValidCreate() with { Email = string.Empty };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_invalid_format_is_error()
    {
        // MIGRATION: mirrors UserInfo.vb Email RegularExpressionValidator(glbEmailRegEx).
        var dto = ValidCreate() with { Email = "bad" };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_over_maximum_length_is_error()
    {
        // A syntactically valid address whose overall length (262) exceeds the 256 cap,
        // so ONLY the MaximumLength(256) rule fires.
        var dto = ValidCreate() with { Email = new string('a', 250) + "@example.com" };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_valid_and_within_maximum_length_has_no_error()
    {
        // Exactly 256 characters ("a" * 244 + "@example.com") and a valid format.
        var dto = ValidCreate() with { Email = new string('a', 244) + "@example.com" };

        _sut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // ---------------------------------------------------------------------
    // Phase 3 (cont.) - DisplayName is optional on the create payload, so the
    // MaximumLength(128) rule only runs .When(!string.IsNullOrEmpty(DisplayName)).
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(null)] // omitted entirely
    [InlineData("")]   // present but empty
    public void DisplayName_null_or_empty_has_no_error(string? displayName)
    {
        var dto = ValidCreate() with { DisplayName = displayName };

        _sut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Theory]
    [InlineData(128, false)] // exactly the MaximumLength(128) cap -> allowed
    [InlineData(129, true)]  // one over the cap -> rejected
    public void DisplayName_length_boundary_is_enforced_when_supplied(int length, bool expectError)
    {
        var dto = ValidCreate() with { DisplayName = new string('a', length) };

        var result = _sut.TestValidate(dto);

        if (expectError)
        {
            result.ShouldHaveValidationErrorFor(x => x.DisplayName);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
        }
    }

    // ---------------------------------------------------------------------
    // Phase 4 - password policy while RandomPassword is false.
    // MIGRATION: UserController.ValidatePassword (MinPasswordLength=7,
    // MinNonAlphanumericCharacters=0) + User.ascx.vb CompareValidator on confirm.
    // ---------------------------------------------------------------------

    [Fact]
    public void Password_empty_reports_required_message()
    {
        var dto = ValidCreate() with { Password = string.Empty, ConfirmPassword = string.Empty };

        _sut.TestValidate(dto)
            .ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password Is Required.");
    }

    [Fact]
    public void Password_shorter_than_minimum_is_error()
    {
        // "Abc12d" is 6 characters, one below the MinimumLength(7) floor.
        var dto = ValidCreate() with { Password = "Abc12d", ConfirmPassword = "Abc12d" };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_at_minimum_length_has_no_error()
    {
        // "Abc123d" is exactly 7 characters and matches the confirmation.
        var dto = ValidCreate() with { Password = "Abc123d", ConfirmPassword = "Abc123d" };

        _sut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void ConfirmPassword_empty_is_error()
    {
        var dto = ValidCreate() with { Password = "Passw0rd", ConfirmPassword = string.Empty };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    [Fact]
    public void ConfirmPassword_mismatch_reports_mismatch_message()
    {
        var dto = ValidCreate() with { Password = "Passw0rd", ConfirmPassword = "different" };

        _sut.TestValidate(dto)
            .ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Passwords do not match.");
    }

    [Fact]
    public void ConfirmPassword_matching_has_no_error()
    {
        var dto = ValidCreate() with { Password = "Passw0rd", ConfirmPassword = "Passw0rd" };

        _sut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    // ---------------------------------------------------------------------
    // Phase 5 - the RandomPassword bypass (the key parity behaviour).
    // MIGRATION: User.ascx.vb (L148-165) skipped all password validation when
    // chkRandom was checked; the server generated the password (L164).
    // ---------------------------------------------------------------------

    [Fact]
    public void RandomPassword_true_bypasses_all_password_rules()
    {
        // Empty password AND empty confirmation would normally raise several errors,
        // but with RandomPassword = true the entire When(!RandomPassword) block is skipped.
        var dto = ValidCreate() with
        {
            RandomPassword = true,
            Password = string.Empty,
            ConfirmPassword = string.Empty,
        };

        var result = _sut.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
        result.ShouldNotHaveValidationErrorFor(x => x.ConfirmPassword);
        result.IsValid.Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // Phase 6 - non-alphanumeric policy contract lock.
    // MinNonAlphanumericCharacters == 0, so a purely alphanumeric password of
    // length >= 7 MUST pass. If a future change requires symbols, this test breaks
    // intentionally to force a conscious policy decision.
    // ---------------------------------------------------------------------

    [Fact]
    public void Alphanumeric_only_password_at_minimum_length_passes()
    {
        // "Abc1234" is 7 characters with zero non-alphanumeric characters.
        var dto = ValidCreate() with { Password = "Abc1234", ConfirmPassword = "Abc1234" };

        _sut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}
