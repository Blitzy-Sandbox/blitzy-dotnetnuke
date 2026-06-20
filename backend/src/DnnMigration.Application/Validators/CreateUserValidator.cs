using FluentValidation;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Validators;

// MIGRATION: User-create validation parity. Reproduces UserInfo.vb DataAnnotation attributes
// (Library/Components/Users/UserInfo.vb) and Website/admin/Users/User.ascx.vb Validate
// (password required when adding a user). UserInfo.vb is the only legacy *Info.vb that carries
// concrete DataAnnotation-style validation attributes, so they are reproduced here faithfully.
// MIGRATION: Duplicate-username and security Q&A checks are stateful and handled in UserService.
public class CreateUserValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserValidator()
    {
        // MIGRATION: UserInfo.Username <Required(True)>.
        RuleFor(x => x.Username)
            .NotEmpty();

        // MIGRATION: User.ascx.vb requires a password when adding a user. Complexity/min-length
        // is provider-config-driven (UserController.ValidatePassword) and enforced in UserService/AuthService, not here.
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
