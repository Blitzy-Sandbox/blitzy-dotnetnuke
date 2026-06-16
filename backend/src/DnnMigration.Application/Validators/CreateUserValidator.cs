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
        // MIGRATION: UserInfo.Username <Required(True)>.
        RuleFor(x => x.Username)
            .NotEmpty();

        // MIGRATION: User.ascx.vb requires a password when adding a user. Complexity/min-length is
        // provider-config-driven (UserController.ValidatePassword) and enforced in UserService/AuthService, not here.
        RuleFor(x => x.Password)
            .NotEmpty();

        // MIGRATION: UserInfo.DisplayName <Required(True), MaxLength(128)>.
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(128);

        // MIGRATION: UserInfo.Email <Required(True), MaxLength(256), RegularExpressionValidator(glbEmailRegEx)> -> EmailAddress().
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        // MIGRATION: UserInfo.FirstName <Required(True), MaxLength(50)>.
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(50);

        // MIGRATION: UserInfo.LastName <Required(True), MaxLength(50)>.
        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(50);
    }
}
