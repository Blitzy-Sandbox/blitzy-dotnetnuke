using System.Text.RegularExpressions;
using FluentValidation;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="ChangePasswordDto"/>, executed for
/// the change-password endpoint (<c>POST /api/users/{id}/change-password</c>)
/// before the payload reaches <c>UserService.ChangePasswordAsync</c>.
/// </summary>
/// <remarks>
/// MIGRATION: reproduces the password rules the legacy DotNetNuke change-password
/// path enforced. The new-password policy mirrors
/// <c>Library/Components/Users/UserController.vb</c> <c>ValidatePassword</c>
/// (lines 1067-1091) — the same policy applied on the "Add User" screen and
/// therefore the same constants used by <see cref="CreateUserDtoValidator"/>
/// (<c>minRequiredPasswordLength = 7</c>, <c>minRequiredNonalphanumericCharacters = 0</c>,
/// taken from <c>Website/development.config</c>). The required-field checks mirror
/// the legacy change-password control's <c>RequiredFieldValidator</c>s on the
/// current- and new-password boxes, and the "new must differ from current" rule
/// preserves the legacy behaviour of rejecting a no-op password change.
///
/// This makes the contract on <see cref="ChangePasswordDto"/> — whose XML remarks
/// state that "required values, complexity rules, and old/new comparison are
/// applied via FluentValidation at the application boundary" — actually enforced.
/// <c>UserService.ChangePasswordAsync</c> additionally verifies the current
/// password against the stored hash (an authorization/authentication check that
/// cannot live in a stateless validator) and retains a defensive whitespace guard
/// on the new password.
///
/// Registered by assembly scanning (<c>AddValidatorsFromAssembly</c>);
/// validation-only, stateless, <c>sealed</c>.
/// </remarks>
public sealed class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    // MIGRATION: identical membership-provider defaults to CreateUserDtoValidator
    // (Website/development.config L239-241). Kept as local constants so the change-
    // password policy is self-documenting and stays aligned with the create policy.
    private const int MinPasswordLength = 7;
    private const int MinNonAlphanumericCharacters = 0;

    /// <summary>
    /// Configures the validation rules for a password-change request.
    /// </summary>
    public ChangePasswordDtoValidator()
    {
        // MIGRATION: RequiredFieldValidator on the "current password" box -> NotEmpty.
        // No complexity rule on the OLD password: it is verified against the stored
        // hash by the service, and may predate the current policy.
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("Current password is required.");

        // MIGRATION: the new password must satisfy the same policy the create path
        // applies (UserController.ValidatePassword) and must differ from the current
        // password (legacy change-password screens rejected a no-op change).
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            // MIGRATION: UserController.ValidatePassword (L1073) rejects passwords shorter
            // than MinPasswordLength (development.config minRequiredPasswordLength=7).
            .MinimumLength(MinPasswordLength)
            // MIGRATION: UserController.ValidatePassword (L1077-1081) requires at least
            // MinNonAlphanumericCharacters non-alphanumeric characters, counted with the
            // regex [^0-9a-zA-Z]. The default minimum is 0, so this rule is satisfied by
            // default; it is retained for parity and future configurability.
            .Must(HasEnoughNonAlphanumeric)
                .WithMessage($"Password must contain at least {MinNonAlphanumericCharacters} non-alphanumeric character(s).")
            // The new password must be a real change, not a re-submission of the current
            // one. This comparison is one of the rules the DTO contract advertises.
            .NotEqual(x => x.OldPassword)
                .WithMessage("New password must be different from the current password.");
    }

    /// <summary>
    /// Returns whether <paramref name="password"/> contains at least
    /// <see cref="MinNonAlphanumericCharacters"/> non-alphanumeric characters.
    /// </summary>
    /// <param name="password">
    /// The candidate new password. Declared nullable so the method is assignable to
    /// the FluentValidation <c>Must</c> predicate delegate under nullable
    /// reference-type analysis; a null or empty value is treated as containing zero
    /// non-alphanumeric characters (the separate <c>NotEmpty</c> rule reports the
    /// missing-value case).
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the non-alphanumeric character count meets or
    /// exceeds the configured minimum; otherwise <see langword="false"/>.
    /// </returns>
    private static bool HasEnoughNonAlphanumeric(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return MinNonAlphanumericCharacters == 0;
        }

        // MIGRATION: exact regex from UserController.ValidatePassword ("[^0-9a-zA-Z]").
        return Regex.Matches(password, "[^0-9a-zA-Z]").Count >= MinNonAlphanumericCharacters;
    }
}
