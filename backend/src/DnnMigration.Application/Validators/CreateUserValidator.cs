using FluentValidation;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Validators;

// MIGRATION: User-create validation parity. Reproduces UserInfo.vb DataAnnotation attributes
// (Library/Components/Users/UserInfo.vb) and Website/admin/Users/User.ascx.vb Validate
// (password required when adding a user). UserInfo.vb is the only legacy *Info.vb that carries
// concrete DataAnnotation-style validation attributes, so they are reproduced here faithfully.
// MIGRATION: Error messages reproduce the legacy Signup.ascx / User.ascx.vb RequiredFieldValidator text
// verbatim (DNN convention: trailing period). DisplayName / MaxLength rules had no single legacy validator
// string, so DNN-style messages are authored for parity.
// MIGRATION: Duplicate-username and security Q&A checks are stateful and handled in UserService.
public class CreateUserValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserValidator()
    {
        // MIGRATION: UserInfo.Username <Required(True)>.
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username Is Required.");

        // MIGRATION: User.ascx.vb requires a password when adding a user. Complexity/min-length
        // is provider-config-driven (UserController.ValidatePassword) and enforced in UserService/AuthService, not here.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password Is Required.");

        // MIGRATION: UserInfo.DisplayName <Required(True), MaxLength(128)>.
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display Name Is Required.")
            .MaximumLength(128).WithMessage("Display Name must be 128 characters or fewer.");

        // MIGRATION: UserInfo.Email <Required(True), MaxLength(256), RegularExpressionValidator(glbEmailRegEx)> -> EmailAddress().
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email Is Required.")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer.")
            .EmailAddress().WithMessage("Email Address is invalid.");

        // MIGRATION: UserInfo.FirstName <Required(True), MaxLength(50)>.
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First Name Is Required.")
            .MaximumLength(50).WithMessage("First Name must be 50 characters or fewer.");

        // MIGRATION: UserInfo.LastName <Required(True), MaxLength(50)>.
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last Name Is Required.")
            .MaximumLength(50).WithMessage("Last Name must be 50 characters or fewer.");
    }
}
