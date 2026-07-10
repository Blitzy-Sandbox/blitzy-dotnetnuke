using DnnMigration.Application.DTOs;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="ChangePasswordDtoValidator"/>, the FluentValidation
/// validator that guards the change-password endpoint
/// (<c>POST /api/users/{id}/change-password</c>).
/// </summary>
/// <remarks>
/// MIGRATION: these tests pin the migrated change-password validation to the legacy
/// DotNetNuke password policy in
/// <c>Library/Components/Users/UserController.vb</c> <c>ValidatePassword</c>
/// (lines 1067-1091) — the same <c>MinPasswordLength = 7</c> /
/// <c>MinNonAlphanumericCharacters = 0</c> policy exercised by
/// <see cref="UserValidatorTests"/> — plus the required-field checks and the
/// new-must-differ-from-old comparison the legacy change-password control enforced.
/// They make the behaviour advertised by the <see cref="ChangePasswordDto"/> XML
/// contract ("required values, complexity rules, and old/new comparison ... via
/// FluentValidation") a tested reality.
/// </remarks>
public class ChangePasswordValidatorTests
{
    private readonly ChangePasswordDtoValidator _sut = new();

    /// <summary>A fully valid change-password baseline that individual tests mutate via <c>with</c>.</summary>
    private static ChangePasswordDto ValidChange() => new()
    {
        OldPassword = "OldPassw0rd",
        NewPassword = "NewPassw0rd",
    };

    [Fact]
    public void Valid_model_has_no_validation_errors()
    {
        var result = _sut.TestValidate(ValidChange());

        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OldPassword_empty_reports_required_message()
    {
        var dto = ValidChange() with { OldPassword = string.Empty };

        _sut.TestValidate(dto)
            .ShouldHaveValidationErrorFor(x => x.OldPassword)
            .WithErrorMessage("Current password is required.");
    }

    [Fact]
    public void NewPassword_empty_reports_required_message()
    {
        var dto = ValidChange() with { NewPassword = string.Empty };

        _sut.TestValidate(dto)
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password is required.");
    }

    [Fact]
    public void NewPassword_shorter_than_minimum_is_error()
    {
        // "Abc12d" is 6 characters, one below the MinimumLength(7) floor.
        var dto = ValidChange() with { NewPassword = "Abc12d" };

        _sut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void NewPassword_at_minimum_length_has_no_length_error()
    {
        // "Abc123d" is exactly 7 characters and differs from the old password.
        var dto = ValidChange() with { NewPassword = "Abc123d" };

        _sut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void NewPassword_equal_to_old_reports_difference_message()
    {
        // MIGRATION: the legacy change-password screen rejected a no-op change; the
        // NotEqual rule reproduces that outcome.
        var dto = ValidChange() with { OldPassword = "SamePassw0rd", NewPassword = "SamePassw0rd" };

        _sut.TestValidate(dto)
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must be different from the current password.");
    }

    [Fact]
    public void NewPassword_different_from_old_has_no_difference_error()
    {
        var dto = ValidChange() with { OldPassword = "OldPassw0rd", NewPassword = "OtherPassw0rd" };

        _sut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void Alphanumeric_only_new_password_at_minimum_length_passes()
    {
        // Non-alphanumeric policy contract lock: MinNonAlphanumericCharacters == 0, so a
        // purely alphanumeric password of length >= 7 (differing from the old password)
        // MUST pass. If a future change requires symbols, this test breaks intentionally.
        var dto = ValidChange() with { OldPassword = "OldPassw0rd", NewPassword = "Abc1234" };

        _sut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.NewPassword);
    }
}
