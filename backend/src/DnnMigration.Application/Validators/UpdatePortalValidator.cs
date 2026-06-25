using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Portal/SiteSettings.ascx (textbox maxlength + the two DataTypeCheck
// CompareValidators) and SiteSettings.ascx.vb -- the legacy portal-settings editor. Behavioral parity (AAP 0.7.1).
// Synchronous-only validator (no repository/data access).
public sealed class UpdatePortalValidator : AbstractValidator<UpdatePortalRequest>
{
    public UpdatePortalValidator()
    {
        // MIGRATION: SiteSettings.ascx txtPortalName ("Title:") has maxlength=128 but NO RequiredFieldValidator --
        // the legacy update screen does NOT require the portal name. For parity we only constrain length and
        // intentionally do NOT add NotEmpty (adding it would reject input that legacy accepted).
        RuleFor(x => x.PortalName)
            .MaximumLength(128);

        // MIGRATION: SiteSettings.ascx txtDescription maxlength=475 (per-form limit; differs from Signup's 500).
        RuleFor(x => x.Description)
            .MaximumLength(475);

        // MIGRATION: SiteSettings.ascx txtKeyWords maxlength=475.
        RuleFor(x => x.KeyWords)
            .MaximumLength(475);

        // MIGRATION: SiteSettings.ascx txtFooterText maxlength=100.
        RuleFor(x => x.FooterText)
            .MaximumLength(100);

        // MIGRATION: SiteSettings.ascx txtHomeDirectory maxlength=100.
        RuleFor(x => x.HomeDirectory)
            .MaximumLength(100);

        // MIGRATION: SiteSettings.ascx processor-username textbox maxlength=50.
        RuleFor(x => x.ProcessorUserId)
            .MaximumLength(50);

        // MIGRATION: SiteSettings.ascx processor-password textbox maxlength=50.
        RuleFor(x => x.ProcessorPassword)
            .MaximumLength(50);

        // MIGRATION: SiteSettings.ascx valExpiryDate (Date) and valHostFee (Currency) were CompareValidator
        // DataTypeCheck validators -- N/A here because ExpiryDate is DateTime? and HostFee is float in the DTO
        // (the type system enforces the type). No rule needed.
    }
}
