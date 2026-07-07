namespace DnnMigration.Application.DTOs;

/// <summary>
/// Read-only projection of a user: identity fields plus composed membership and
/// profile projections. Returned by the user read endpoints (GET /api/users,
/// GET /api/users/{id}).
/// MIGRATION: mapped from the legacy DotNetNuke UserInfo entity
/// (Library/Components/Users/UserInfo.vb) composed with its Membership and Profile.
/// Credential material (Password / PasswordAnswer / PasswordQuestion / hash / salt)
/// is intentionally excluded from this read boundary — those live only on the
/// dedicated create/change-password write payloads. This DTO carries no behaviour,
/// mapping, or validation; those concerns live in the sibling Mapping/ and
/// Validators/ folders and in the API layer respectively. The property names/types
/// mirror the frontend read model (frontend/src/app/core/models/user.model.ts,
/// interface User); System.Text.Json emits camelCase on the wire, preserving the
/// legacy acronym casing (userID, portalID, affiliateID, im).
/// </summary>
public record UserDto
{
    public int UserID { get; init; }
    public int PortalID { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsSuperUser { get; init; }
    public int AffiliateID { get; init; }

    /// <summary>Role names the user belongs to.</summary>
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    /// <summary>Composed, credential-safe membership/account-state projection.</summary>
    public MembershipDto Membership { get; init; } = new();

    /// <summary>Composed profile (address/contact/locale) projection.</summary>
    public ProfileDto Profile { get; init; } = new();
}
