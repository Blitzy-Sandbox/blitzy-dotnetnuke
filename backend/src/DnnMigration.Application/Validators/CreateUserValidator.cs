using FluentValidation;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreateUserDto"/>. Enforces the field-level rules
/// required to create a new user, reproducing the legacy DNN user-creation validation so the
/// migrated API preserves behavioral parity with the original Web Forms administration screens.
/// </summary>
/// <remarks>
/// Stateless, structural validation only. Stateful checks (duplicate username, security
/// question/answer requirements) and provider-configurable password complexity/min-length are
/// intentionally NOT performed here; they are enforced in the service layer (UserService /
/// AuthService) where repository and configuration access is available.
/// </remarks>
// MIGRATION: User-create validation parity. Rules reproduce the DataAnnotation-style attributes on
// Library/Components/Users/UserInfo.vb and the password requirement in
// Website/admin/Users/User.ascx.vb Validate (password required when adding a user).
// MIGRATION: Duplicate-username and security Q&A checks are stateful and handled in UserService.
public class CreateUserValidator : AbstractValidator<CreateUserDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateUserValidator"/> class and registers
    /// the field-level rules for creating a user.
    /// </summary>
    public CreateUserValidator()
    {
        // MIGRATION: UserInfo.Username <Required(True)>. Message from User.ascx.resx ("Enter a username").
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Enter a username");

        // MIGRATION: User.ascx.vb requires a password when adding a user (message from User.ascx.resx).
        // Complexity/min-length is provider-config-driven (UserController.ValidatePassword) and enforced
        // in UserService/AuthService, not here.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("You must enter a new password.");

        // MIGRATION: UserInfo.DisplayName <Required(True), MaxLength(128)>.
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Enter a display name")
            .MaximumLength(128).WithMessage("Display Name must be 128 characters or fewer.");

        // MIGRATION: UserInfo.Email <Required(True), MaxLength(256), RegularExpressionValidator(glbEmailRegEx)> -> EmailAddress().
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Enter an Email address")
            .MaximumLength(256).WithMessage("Email Address must be 256 characters or fewer.")
            .EmailAddress().WithMessage("Enter a valid Email address");

        // MIGRATION: UserInfo.FirstName <Required(True), MaxLength(50)>.
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Enter a First Name")
            .MaximumLength(50).WithMessage("First Name must be 50 characters or fewer.");

        // MIGRATION: UserInfo.LastName <Required(True), MaxLength(50)>.
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Enter a Last Name")
            .MaximumLength(50).WithMessage("Last Name must be 50 characters or fewer.");
    }
}
