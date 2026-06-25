using FluentValidation;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Users/User.ascx(.vb) -- the dnn:propertyeditorcontrol field metadata
// declared on UserInfo.vb (Required/MaxLength/RegularExpressionValidator) plus the valPassword CustomValidator --
// and ManageUsers.ascx.vb. Behavioral parity (AAP 0.7.1): identical required-ness, length, email format, password
// rules and error strings. Synchronous-only validator (no repository/data access): duplicate-username/email
// detection (UserController GetUserByUserName / GetUserByEmail) is a multi-entity check enforced in UserService.
public sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    // MIGRATION: glbEmailRegEx from Library/Components/Shared/Globals.vb (L132), preserved verbatim for parity.
    private const string EmailRegex = @"\b[a-zA-Z0-9._%\-+']+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,4}\b";

    public CreateUserValidator()
    {
        // MIGRATION: API contract -- PortalId is the multi-tenant scope (legacy from PortalSettings context); must be a valid (>= 0) portal id.
        RuleFor(x => x.PortalId)
            .GreaterThanOrEqualTo(0);

        // MIGRATION: UserInfo.Username <Required(True)>. Legacy membership status text "InvalidUserName".
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("The username specified is invalid.  Please specify a valid username.");

        // MIGRATION: UserInfo.DisplayName <Required(True), MaxLength(128)>.
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(128);

        // MIGRATION: UserInfo.Email <Required(True), MaxLength(256), RegularExpressionValidator(glbEmailRegEx)>.
        // Membership status text "InvalidEmail" used for the format failure. Matches passes on null, so a null email
        // surfaces the NotEmpty (required) failure instead.
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .Matches(EmailRegex)
                .WithMessage("The email address specified is invalid.  Please specify a valid email address.");

        // MIGRATION: UserInfo.FirstName <Required(True), MaxLength(50)>.
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(50);

        // MIGRATION: UserInfo.LastName <Required(True), MaxLength(50)>.
        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(50);

        // MIGRATION: User.ascx.vb Validate() -- password rules apply only when a password is supplied. The legacy
        // chkRandom path auto-generates the password server-side and skips these checks, so Password is optional here
        // and the rules are conditional. txtPassword/txtConfirm maxlength=20; UserController.ValidatePassword enforced
        // MinPasswordLength (release.config minRequiredPasswordLength=7).
        When(x => !string.IsNullOrEmpty(x.Password), () =>
        {
            // MIGRATION: ValidatePassword length check. release.config minRequiredPasswordLength=7,
            // minRequiredNonalphanumericCharacters=0. The legacy InvalidPassword message template left the
            // [NoneAlphabet] token literal (only [PasswordLength] was substituted) -- a documented legacy quirk; here
            // both tokens are resolved (7 and 0) for a clean API message.
            RuleFor(x => x.Password)
                .MinimumLength(7)
                    .WithMessage("The password specified is invalid.  Please specify a valid password.  Passwords must be at least 7 characters in length and contain at least 0 non-alphanumeric characters.")
                .MaximumLength(20);

            // MIGRATION: User.ascx.vb txtPassword vs txtConfirm equality check -> UserCreateStatus.PasswordMismatch.
            RuleFor(x => x.Confirm)
                .Equal(x => x.Password).WithMessage("The Password and Confirmation Passwords do not match");
        });
    }
}
