using FluentValidation;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Users/User.ascx(.vb) edit branch + UserInfo.vb property-editor metadata.
// Username is the immutable login identity (UserInfo.Username <IsReadOnly(True)>) and is not part of the update body;
// password changes are a separate secured flow. Behavioral parity (AAP 0.7.1). Synchronous-only validator (no
// repository/data access): duplicate-email detection is enforced in UserService.
public sealed class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    // MIGRATION: glbEmailRegEx from Library/Components/Shared/Globals.vb (L132), preserved verbatim.
    private const string EmailRegex = @"\b[a-zA-Z0-9._%\-+']+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,4}\b";

    public UpdateUserValidator()
    {
        // MIGRATION: UserInfo.Email <Required(True), MaxLength(256), RegularExpressionValidator(glbEmailRegEx)>.
        // Membership status text "InvalidEmail" for the format failure (Matches passes on null -> NotEmpty surfaces required).
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .Matches(EmailRegex)
                .WithMessage("The email address specified is invalid.  Please specify a valid email address.");

        // MIGRATION: UserInfo.DisplayName <Required(True), MaxLength(128)>.
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(128);

        // MIGRATION: UserInfo.FirstName <Required(True), MaxLength(50)>.
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(50);

        // MIGRATION: UserInfo.LastName <Required(True), MaxLength(50)>.
        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(50);

        // MIGRATION: IsApproved (legacy "Authorize" checkbox) and LockedOut (admin unlock) are booleans with no
        // legacy validation rule -- intentionally unvalidated.
    }
}
