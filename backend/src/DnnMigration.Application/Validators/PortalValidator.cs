using FluentValidation;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreatePortalDto"/>, executed for
/// <c>POST /api/portals</c> before the payload provisions a new portal record and its
/// optional initial administrator user.
/// </summary>
/// <remarks>
/// MIGRATION: mirrors the field-level <c>RequiredFieldValidator</c>s declared on the
/// legacy DotNetNuke portal-creation screen <c>Website/admin/Portal/signup.ascx</c>
/// (Portal Name, First Name, Last Name, Username, Password and Email) together with the
/// portal alias captured by <c>Website/admin/Portal/editportalalias.ascx</c>
/// (<c>txtAlias</c>). Behavioural parity - not "improvement" - is the goal: each rule
/// reproduces the outcome the original Web Forms validator produced for the same input,
/// and the user-visible error messages are preserved verbatim through <c>WithMessage</c>
/// calls.
///
/// The legacy <c>Library/Components/Portal/PortalTemplateValidator.vb</c> is an XML/XSD
/// schema validator (it inherits <c>XmlValidatorBase</c> and validates a portal-template
/// file); portal-template / XML processing is out of scope for this migration, so none of
/// its schema logic is ported here.
///
/// This type performs validation only - it holds no state, performs no data access, and
/// applies no business orchestration, entity projection or response shaping (those are
/// Application-service and API-layer concerns). It is declared <c>public</c> so the API
/// host can discover and register it through FluentValidation's assembly scanning
/// (<c>AddValidatorsFromAssembly</c>) at start-up, and <c>sealed</c> because it is not
/// designed to be extended.
/// </remarks>
public sealed class CreatePortalDtoValidator : AbstractValidator<CreatePortalDto>
{
    /// <summary>
    /// Configures the field-level validation rules for a portal-creation request.
    /// </summary>
    public CreatePortalDtoValidator()
    {
        // MIGRATION: signup.ascx valPortalName RequiredFieldValidator
        // (errormessage="Portal Name Is Required.") on txtPortalName, whose
        // maxlength="128" attribute is preserved as MaximumLength(128).
        RuleFor(x => x.PortalName)
            .NotEmpty().WithMessage("Portal Name Is Required.")
            .MaximumLength(128);

        // MIGRATION: signup.ascx valFirstName / valLastName / valUsername / valPassword
        // RequiredFieldValidators; the user-visible messages are preserved verbatim.
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First Name Is Required.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last Name Is Required.");
        RuleFor(x => x.Username).NotEmpty().WithMessage("Username Is Required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password Is Required.");

        // MIGRATION: signup.ascx valConfirm RequiredFieldValidator on txtConfirm plus the
        // txtPassword/txtConfirm equality gate enforced in signup.ascx.vb (L219-221:
        // "If txtPassword.Text <> txtConfirm.Text"). The required message is preserved verbatim
        // from valConfirm's errormessage and the mismatch message from Signup.ascx.resx
        // (InvalidPassword.Text = "The Password Values Entered Do Not Match.").
        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password Confirmation Is Required.")
            .Equal(x => x.Password).WithMessage("The Password Values Entered Do Not Match.");

        // MIGRATION: signup.ascx required-only; .EmailAddress() mirrors the email regex
        // (glbEmailRegEx) enforced on UserInfo.Email for parity. The "Email Is Required."
        // message is preserved verbatim from valEmail's errormessage attribute.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email Is Required.")
            .EmailAddress();

        // MIGRATION: PortalAlias is the initial HTTP alias required to provision the portal
        // (editportalalias.ascx txtAlias, maxlength="255"). The legacy screen's code-behind
        // stripped the "://" scheme prefix and "\" backslashes from the entered value, so an
        // alias containing whitespace or a backslash was never persisted. That constraint is
        // reproduced here as a character rule rejecting any whitespace or backslash, and the
        // 255-character cap preserves the maxlength.
        RuleFor(x => x.PortalAlias)
            .NotEmpty()
            .MaximumLength(255)
            .Matches(@"^[^\s\\]+$").WithMessage("Portal Alias contains invalid characters.");
    }
}

/// <summary>
/// FluentValidation validator for <see cref="UpdatePortalDto"/>, executed for
/// <c>PUT /api/portals/{id}</c> before the payload is projected onto the existing portal
/// record.
/// </summary>
/// <remarks>
/// MIGRATION: mirrors the editable fields on the legacy Site Settings screen
/// <c>Website/admin/Portal/sitesettings.ascx</c>. That screen's numeric/date validators
/// are <c>CompareValidator</c>s with <c>Operator="DataTypeCheck"</c> - a Date check on
/// <c>txtExpiryDate</c> and a Currency check on <c>txtHostFee</c> - which are pure type
/// checks already guaranteed by this DTO's strong typing (<see cref="UpdatePortalDto.ExpiryDate"/>
/// is <see cref="System.DateTime"/> and <see cref="UpdatePortalDto.HostFee"/> is
/// <c>float</c>); they therefore require no explicit rule. The quota text boxes
/// (<c>txtHostSpace</c> / <c>txtPageQuota</c> / <c>txtUserQuota</c>, each
/// <c>maxlength="6"</c>) represent non-negative integers.
///
/// Behavioural parity is the goal; this type performs validation only and, like its
/// create-side counterpart, is <c>public sealed</c> so FluentValidation's assembly
/// scanning can discover and register it.
/// </remarks>
public sealed class UpdatePortalDtoValidator : AbstractValidator<UpdatePortalDto>
{
    /// <summary>
    /// Configures the field-level validation rules for a portal-update request.
    /// </summary>
    public UpdatePortalDtoValidator()
    {
        // MIGRATION: sitesettings.ascx txtPortalName (maxlength="128"). PortalName is a
        // required, non-nullable portal attribute, so NotEmpty is retained and the 128
        // cap preserves the maxlength.
        RuleFor(x => x.PortalName)
            .NotEmpty()
            .MaximumLength(128);

        // MIGRATION: sitesettings.ascx CompareValidator(DataTypeCheck Date/Currency) is
        // satisfied by DTO typing (DateTime/float); only non-negative quota/fee range
        // rules are added. HostFee is float, so GreaterThanOrEqualTo(0) binds the float
        // overload (the int literal 0 converts implicitly to float).
        RuleFor(x => x.HostFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.HostSpace).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageQuota).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UserQuota).GreaterThanOrEqualTo(0);
    }
}
