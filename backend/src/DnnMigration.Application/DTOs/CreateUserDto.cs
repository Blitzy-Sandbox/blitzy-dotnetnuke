namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload used to create a new user via <c>POST /api/users</c>.
/// </summary>
/// <remarks>
/// MIGRATION: mirrors the legacy DotNetNuke "Add User" screen
/// <c>Website/admin/Users/User.ascx</c> - the checkbox controls
/// <c>chkAuthorize</c>/<c>chkNotify</c>/<c>chkRandom</c> and the text controls
/// <c>txtPassword</c>/<c>txtConfirm</c>/<c>txtQuestion</c>/<c>txtAnswer</c> - and
/// projects the identity fields of <c>UserInfo</c>
/// (<c>Library/Components/Users/UserInfo.vb</c>) together with the credential
/// fields of <c>UserMembership</c>
/// (<c>Library/Components/Users/Membership/UserMembership.vb</c>) onto a single
/// write model.
///
/// This type is a pure transport contract: it carries no behaviour, no data
/// access and no validation attributes. Field-level rules (required fields,
/// maximum lengths, e-mail format, and the password/confirm-password match)
/// are enforced by the sibling FluentValidation validator <c>UserValidator</c>,
/// and the payload is projected onto the domain <c>User</c> aggregate by
/// <c>MappingProfile</c>.
///
/// Passwords are intentionally present because this is a create payload; the
/// confirm-password, security question and security answer are optional and
/// therefore nullable. Non-nullable string members default to
/// <see cref="string.Empty"/> so the record is always constructed in a valid,
/// non-null state.
/// </remarks>
public record CreateUserDto
{
    /// <summary>
    /// Login name for the new account.
    /// MIGRATION: <c>txtUsername</c> -&gt; <c>UserInfo.Username</c>.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// User's given (first) name.
    /// MIGRATION: <c>txtFirstName</c> -&gt; <c>UserInfo.FirstName</c> (stored on the profile).
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// User's family (last) name.
    /// MIGRATION: <c>txtLastName</c> -&gt; <c>UserInfo.LastName</c> (stored on the profile).
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Optional display name; when omitted a display name is derived downstream.
    /// MIGRATION: <c>txtDisplayName</c> -&gt; <c>UserInfo.DisplayName</c>.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// E-mail address for the new account.
    /// MIGRATION: <c>txtEmail</c> -&gt; <c>UserInfo.Email</c>.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Initial password for the account. Ignored when <see cref="RandomPassword"/> is <see langword="true"/>.
    /// MIGRATION: <c>txtPassword</c> -&gt; <c>UserMembership.Password</c>.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Optional password confirmation, supplied by the UI purely for the match check performed in <c>UserValidator</c>.
    /// MIGRATION: <c>txtConfirm</c>.
    /// </summary>
    public string? ConfirmPassword { get; init; }

    /// <summary>
    /// Optional password-recovery question.
    /// MIGRATION: <c>txtQuestion</c> -&gt; <c>UserMembership.PasswordQuestion</c>.
    /// </summary>
    public string? PasswordQuestion { get; init; }

    /// <summary>
    /// Optional password-recovery answer.
    /// MIGRATION: <c>txtAnswer</c> -&gt; <c>UserMembership.PasswordAnswer</c>.
    /// </summary>
    public string? PasswordAnswer { get; init; }

    /// <summary>
    /// Identifier of the portal the user is created under, supplied by the request context/route rather than a form field.
    /// MIGRATION: <c>UserInfo.PortalID</c>.
    /// </summary>
    public int PortalID { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the account is authorised (approved) immediately upon creation.
    /// MIGRATION: <c>chkAuthorize</c> (defaulted to checked on the legacy screen).
    /// </summary>
    public bool Authorize { get; init; }

    /// <summary>
    /// When <see langword="true"/>, a notification e-mail is sent to the newly created user.
    /// MIGRATION: <c>chkNotify</c> (defaulted to checked on the legacy screen).
    /// </summary>
    public bool Notify { get; init; }

    /// <summary>
    /// When <see langword="true"/>, a random password is generated server-side and <see cref="Password"/> is ignored.
    /// MIGRATION: <c>chkRandom</c> (defaulted to checked on the legacy screen).
    /// </summary>
    public bool RandomPassword { get; init; }
}
