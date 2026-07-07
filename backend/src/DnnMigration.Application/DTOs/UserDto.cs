namespace DnnMigration.Application.DTOs;

/// <summary>
/// Read-only projection of a user returned by the Users API
/// (<c>GET /api/users</c> and <c>GET /api/users/{id}</c>) and reused as the
/// user payload of <c>CurrentUserDto</c> (<c>GET /api/auth/me</c>).
/// </summary>
/// <remarks>
/// MIGRATION: mapped from the legacy VB.NET entity <c>UserInfo</c>
/// (Library/Components/Users/UserInfo.vb), which composes the legacy
/// <c>UserMembership</c> (Library/Components/Users/Membership/UserMembership.vb)
/// and <c>UserProfile</c> (Library/Components/Users/Profile/UserProfile.vb)
/// entities. This is an outbound Data Transfer Object at the Application
/// boundary: it is projected from the <c>User</c> domain entity via AutoMapper
/// and never carries business logic, data-access, or validation concerns.
/// Property names and CLR types intentionally mirror the source entity
/// one-for-one so AutoMapper requires no custom value converters.
/// <para>
/// SECURITY: the read boundary deliberately omits every credential field
/// (<c>Password</c>, <c>PasswordAnswer</c>, <c>PasswordQuestion</c>) that the
/// legacy <c>UserMembership</c> exposed. The nested <see cref="MembershipDto"/>
/// likewise excludes them, so no secret can ever be serialized to a client.
/// </para>
/// <para>
/// The legacy <c>UserInfo.FirstName</c>/<c>LastName</c> properties delegated to
/// the profile (<c>Profile.FirstName</c>/<c>Profile.LastName</c>); they are
/// surfaced here at the top level to preserve that read contract, while the
/// remaining profile fields live on the nested <see cref="ProfileDto"/>. The
/// legacy computed <c>FullName</c> property is intentionally not projected.
/// </para>
/// </remarks>
public record UserDto
{
    /// <summary>
    /// Unique identifier of the user. MIGRATION: from <c>UserInfo.UserID</c> (VB <c>Integer</c>).
    /// </summary>
    public int UserID { get; init; }

    /// <summary>
    /// Identifier of the portal the user belongs to. MIGRATION: from <c>UserInfo.PortalID</c> (VB <c>Integer</c>).
    /// </summary>
    public int PortalID { get; init; }

    /// <summary>
    /// Login name of the user. MIGRATION: from <c>UserInfo.Username</c> (VB <c>String</c>).
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Friendly display name of the user. MIGRATION: from <c>UserInfo.DisplayName</c> (VB <c>String</c>).
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// First name of the user. MIGRATION: from <c>UserInfo.FirstName</c>, which the legacy entity
    /// delegated to <c>Profile.FirstName</c>; surfaced here at the top level to preserve the read contract.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Last name of the user. MIGRATION: from <c>UserInfo.LastName</c>, which the legacy entity
    /// delegated to <c>Profile.LastName</c>; surfaced here at the top level to preserve the read contract.
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Email address of the user. MIGRATION: from <c>UserInfo.Email</c> (VB <c>String</c>).
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the user is a super user (host-level administrator).
    /// MIGRATION: from <c>UserInfo.IsSuperUser</c> (VB <c>Boolean</c>).
    /// </summary>
    public bool IsSuperUser { get; init; }

    /// <summary>
    /// Identifier of the affiliate that referred the user. MIGRATION: from <c>UserInfo.AffiliateID</c> (VB <c>Integer</c>).
    /// </summary>
    public int AffiliateID { get; init; }

    /// <summary>
    /// Names of the roles the user is a member of. MIGRATION: from <c>UserInfo.Roles</c> (VB <c>String()</c>).
    /// Initialized to an empty array so the read boundary never returns a null collection.
    /// </summary>
    public string[] Roles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Membership/account-state projection for the user. MIGRATION: from <c>UserInfo.Membership</c>
    /// (<c>UserMembership</c>); credential fields are intentionally excluded (see <see cref="MembershipDto"/>).
    /// </summary>
    public MembershipDto Membership { get; init; } = new();

    /// <summary>
    /// Profile (address/contact/locale) projection for the user. MIGRATION: from <c>UserInfo.Profile</c>
    /// (<c>UserProfile</c>); see <see cref="ProfileDto"/>.
    /// </summary>
    public ProfileDto Profile { get; init; } = new();
}
