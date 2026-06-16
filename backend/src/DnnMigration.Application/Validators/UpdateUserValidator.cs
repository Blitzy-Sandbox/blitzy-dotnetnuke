using FluentValidation;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Validators;

// MIGRATION: User-update validation parity (UserController.vb update path). Field rules from
// UserInfo.vb attributes. Username and Password are excluded from UpdateUserDto by design
// (password changes flow through a dedicated AuthService path).
public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserValidator()
    {
        // MIGRATION: update targets an existing user — key must be valid (> 0).
        RuleFor(x => x.UserID)
            .GreaterThan(0).WithMessage("A valid user identifier is required.");

        // MIGRATION: UserInfo.DisplayName <Required(True), MaxLength(128)>.
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Enter a display name")
            .MaximumLength(128).WithMessage("Display Name must be 128 characters or fewer.");

        // MIGRATION: UserInfo.Email <Required(True), MaxLength(256), RegularExpressionValidator(glbEmailRegEx)>.
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

        // MIGRATION: Username and Password are immutable on update (excluded from UpdateUserDto); password changes flow through a dedicated AuthService path.
    }
}
