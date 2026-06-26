using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Portal/Signup.ascx (RequiredFieldValidators + textbox maxlength) and
// Signup.ascx.vb — the legacy portal-creation form. Behavioral parity (AAP 0.7.1): identical required-ness, length
// limits and error message strings. Synchronous-only validator (no repository/data access). The actual admin-user
// CREATION (persisting the user + credential + assigning portal.AdministratorId) is a multi-entity concern performed
// in PortalService; this validator only enforces the SHAPE of the optional admin-bootstrap group.
// MIGRATION (CP1 review PortalService #3): the legacy Signup form required FirstName/LastName/Username/Password/Email
// for the portal Administrator. The migrated CreatePortalRequest surfaces these as the OPTIONAL Admin* group; here
// they are validated as a REQUIRED GROUP — required together if and only if any one is supplied (the
// .When(AdminProvisioningRequested) guard) — mirroring the exact-grouping pattern used for the Role billing/trial
// group. When no admin field is supplied the group is skipped entirely, so the base portal request stays valid.
public sealed class CreatePortalValidator : AbstractValidator<CreatePortalRequest>
{
    // MIGRATION: group trigger — admin provisioning is "requested" when ANY admin field is non-empty. Uses the same
    // non-trimming IsNullOrEmpty semantics as the RequiredFieldValidator parity below (whitespace counts as present).
    private static bool AdminProvisioningRequested(CreatePortalRequest r) =>
        !string.IsNullOrEmpty(r.AdminUsername)
        || !string.IsNullOrEmpty(r.AdminPassword)
        || !string.IsNullOrEmpty(r.AdminFirstName)
        || !string.IsNullOrEmpty(r.AdminLastName)
        || !string.IsNullOrEmpty(r.AdminEmail);

    public CreatePortalValidator()
    {
        // MIGRATION: Signup.ascx valPortalName RequiredFieldValidator on txtPortalName; txtPortalName
        // maxlength=128 -> MaximumLength(128). The ASP.NET RequiredFieldValidator fails only when the control's
        // value equals its (default empty) InitialValue and DOES NOT trim, so a whitespace-only value (" ")
        // PASSES in the legacy form. FluentValidation .NotEmpty() additionally rejects whitespace (it treats
        // IsNullOrWhiteSpace as empty), which would TIGHTEN behavior — forbidden by AAP §0.7.2 (no behavioral
        // improvement during migration) and flagged by the CP1 review (CreatePortalValidator #1). To preserve
        // EXACT parity, the required-ness is expressed as Must(value => !string.IsNullOrEmpty(value)): it rejects
        // null and "" (the legacy InitialValue) but ACCEPTS whitespace exactly as RequiredFieldValidator did.
        RuleFor(x => x.PortalName)
            .Must(value => !string.IsNullOrEmpty(value)).WithMessage("Portal Name Is Required.")
            .MaximumLength(128);

        // MIGRATION: Signup.ascx valEmail RequiredFieldValidator on txtEmail; txtEmail maxlength=100. Same exact
        // RequiredFieldValidator parity as PortalName above — Must(value => !string.IsNullOrEmpty(value)) rejects
        // only null/"" and accepts whitespace (no .NotEmpty() tightening). Legacy Signup had NO email-format
        // RegularExpressionValidator, so none is added here (faithful parity).
        RuleFor(x => x.Email)
            .Must(value => !string.IsNullOrEmpty(value)).WithMessage("Email Is Required.")
            .MaximumLength(100);

        // MIGRATION: Signup.ascx txtDescription maxlength=500 (no server-side message in legacy; length guard only).
        RuleFor(x => x.Description)
            .MaximumLength(500);

        // MIGRATION: Signup.ascx txtKeyWords maxlength=500.
        RuleFor(x => x.KeyWords)
            .MaximumLength(500);

        // MIGRATION: Signup.ascx txtHomeDirectory maxlength=100.
        RuleFor(x => x.HomeDirectory)
            .MaximumLength(100);

        // --- MIGRATION (CP1 review PortalService #3): optional portal-administrator group ---
        // Required together ONLY when admin provisioning is requested (any Admin* field present). Each rule uses the
        // same Must(value => !string.IsNullOrEmpty(value)) parity as PortalName/Email above (the legacy admin
        // RequiredFieldValidators also did not trim), so a whitespace-only value is treated as supplied — no
        // .NotEmpty() tightening (AAP §0.7.2 / CP1 review CreatePortalValidator #1).
        RuleFor(x => x.AdminUsername)
            .Must(value => !string.IsNullOrEmpty(value)).WithMessage("Administrator User Name Is Required.")
            .When(AdminProvisioningRequested);

        RuleFor(x => x.AdminPassword)
            .Must(value => !string.IsNullOrEmpty(value)).WithMessage("Administrator Password Is Required.")
            .When(AdminProvisioningRequested);

        RuleFor(x => x.AdminFirstName)
            .Must(value => !string.IsNullOrEmpty(value)).WithMessage("Administrator First Name Is Required.")
            .When(AdminProvisioningRequested);

        RuleFor(x => x.AdminLastName)
            .Must(value => !string.IsNullOrEmpty(value)).WithMessage("Administrator Last Name Is Required.")
            .When(AdminProvisioningRequested);

        RuleFor(x => x.AdminEmail)
            .Must(value => !string.IsNullOrEmpty(value)).WithMessage("Administrator Email Is Required.")
            .When(AdminProvisioningRequested);
    }
}
