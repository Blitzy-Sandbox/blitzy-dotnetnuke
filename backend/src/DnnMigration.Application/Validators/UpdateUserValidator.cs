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
            .GreaterThan(0);

        // MIGRATION: UserInfo.DisplayName <Required(True), MaxLength(128)>.
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(128);

        // MIGRATION: UserInfo.Email <Required(True), MaxLength(256), RegularExpressionValidator(glbEmailRegEx)>.
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

        // MIGRATION: Username and Password are immutable on update (excluded from UpdateUserDto); password changes flow through a dedicated AuthService path.
    }
}
