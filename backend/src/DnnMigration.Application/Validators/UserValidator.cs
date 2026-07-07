using System.Text.RegularExpressions;
using FluentValidation;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreateUserDto"/>, executed for
/// <c>POST /api/users</c> before the payload is projected onto the domain
/// <c>User</c> aggregate.
/// </summary>
/// <remarks>
/// MIGRATION: mirrors the legacy DotNetNuke "Add User" screen
/// (<c>Website/admin/Users/User.ascx</c> and its code-behind
/// <c>User.ascx.vb</c> <c>Validate()</c> method, lines 133-195), the
/// <c>Required</c>/<c>MaxLength</c>/<c>RegularExpressionValidator</c> property
/// attributes declared on <c>Library/Components/Users/UserInfo.vb</c>, and the
/// password policy implemented by
/// <c>Library/Components/Users/UserController.vb</c> <c>ValidatePassword</c>
/// (lines 1067-1091).
///
/// Behavioural parity, not "improvement", is the goal: the rules below reproduce
/// the outcomes the legacy Web Forms validators produced for the same inputs.
/// This type performs validation only - it holds no state, performs no data
/// access, and applies no business logic beyond field-level validation.
///
/// The class is declared <c>public</c> so the API host can discover and register
/// it through FluentValidation's assembly-scanning
/// (<c>AddValidatorsFromAssembly</c>) at start-up, and <c>sealed</c> because it is
/// not designed to be extended.
/// </remarks>
public sealed class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    // MIGRATION: membership-provider defaults taken from Website/development.config
    // (L239-241): minRequiredPasswordLength=7, minRequiredNonalphanumericCharacters=0,
    // requiresQuestionAndAnswer=false. Modelled as compile-time constants so the
    // policy is expressed in one place and stays trivially configurable later.
    private const int MinPasswordLength = 7;
    private const int MinNonAlphanumericCharacters = 0;

    /// <summary>
    /// Configures the validation rules for a user-creation request.
    /// </summary>
    public CreateUserDtoValidator()
    {
        // MIGRATION: UserInfo.vb marks Username as Required. The legacy property
        // editor rendered a RequiredFieldValidator; here that becomes NotEmpty.
        RuleFor(x => x.Username).NotEmpty();

        // MIGRATION: UserInfo.vb FirstName/LastName are Required with MaxLength 50.
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);

        // MIGRATION: UserInfo.vb Email is Required, MaxLength 256, and validated
        // by a RegularExpressionValidator (glbEmailRegEx). EmailAddress() supplies
        // the equivalent format check; the length check preserves the 256 cap.
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        // MIGRATION: UserInfo.vb declares DisplayName MaxLength 128. On the create
        // payload DisplayName is optional (nullable on CreateUserDto and derived
        // downstream when omitted), so the length rule only runs when a value is
        // supplied; no NotEmpty/Required rule is added.
        RuleFor(x => x.DisplayName)
            .MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.DisplayName));

        // MIGRATION: User.ascx.vb Validate() (L148-165) only validates the password
        // when the record is an add-user AND chkRandom is unchecked; when chkRandom
        // is checked the server generates a random password (L164) so no password or
        // confirm rules apply. That maps to gating every password rule on
        // !RandomPassword.
        When(x => !x.RandomPassword, () =>
        {
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password Is Required.")
                // MIGRATION: UserController.ValidatePassword (L1073) rejects passwords
                // shorter than MinPasswordLength (development.config minRequiredPasswordLength=7).
                .MinimumLength(MinPasswordLength)
                // MIGRATION: UserController.ValidatePassword (L1077-1081) requires at least
                // MinNonAlphanumericCharacters non-alphanumeric characters, counted with the
                // regex [^0-9a-zA-Z]. The default minimum is 0, so this rule is satisfied by
                // default; it is retained for parity and future configurability.
                .Must(HasEnoughNonAlphanumeric)
                    .WithMessage($"Password must contain at least {MinNonAlphanumericCharacters} non-alphanumeric character(s).");
            // MIGRATION: UserController.ValidatePassword (L1084-1087) additionally applies
            // PasswordStrengthRegularExpression when it is non-empty. It is empty by default in
            // development.config, so no .Matches(...) rule is emitted; a configurable strength
            // regex rule would attach here if that setting were surfaced.

            // MIGRATION: User.ascx.vb (L152) compares txtPassword to txtConfirm via a
            // CompareValidator; that becomes NotEmpty + Equal on the confirmation field.
            RuleFor(x => x.ConfirmPassword)
                .NotEmpty()
                .Equal(x => x.Password).WithMessage("Passwords do not match.");
        });

        // MIGRATION: User.ascx.vb (L168) only enforces the password question/answer when the
        // membership provider sets RequiresQuestionAndAnswer, which defaults to false in
        // Website/development.config. No question/answer rules are added by default.
    }

    /// <summary>
    /// Returns whether <paramref name="password"/> contains at least
    /// <see cref="MinNonAlphanumericCharacters"/> non-alphanumeric characters.
    /// </summary>
    /// <param name="password">
    /// The candidate password. Declared nullable so the method is assignable to the
    /// FluentValidation <c>Must</c> predicate delegate under nullable reference-type
    /// analysis; a null or empty value is treated as containing zero non-alphanumeric
    /// characters.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the non-alphanumeric character count meets or
    /// exceeds the configured minimum; otherwise <see langword="false"/>.
    /// </returns>
    private static bool HasEnoughNonAlphanumeric(string? password)
    {
        // An empty/absent password contributes zero non-alphanumeric characters, so it
        // only satisfies the rule when the required minimum is itself zero. (NotEmpty
        // handles the "password is missing" case separately.)
        if (string.IsNullOrEmpty(password))
        {
            return MinNonAlphanumericCharacters == 0;
        }

        // MIGRATION: exact regex from UserController.ValidatePassword ("[^0-9a-zA-Z]").
        return Regex.Matches(password, "[^0-9a-zA-Z]").Count >= MinNonAlphanumericCharacters;
    }
}
