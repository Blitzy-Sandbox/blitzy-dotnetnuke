namespace DnnMigration.Application.DTOs.User;

// MIGRATION: Read-model response DTO projected from DnnMigration.Domain.Entities.User
// (legacy DotNetNuke.Entities.Users.UserInfo, with non-credential account-status fields originally on
// UserMembership.vb). Returned by GET /api/users and /api/users/{id}; also the element type of the
// service-layer PagedResult<UserResponse>.
// MIGRATION (SECURITY, AAP 0.7.6): This response NEVER carries credential material — no Password,
// PasswordQuestion, PasswordAnswer, password hash, or password-change timestamp (legacy
// UserMembership.LastPasswordChangeDate is deliberately omitted). Credentials live only in
// Infrastructure/Identity (BCrypt) and are surfaced solely through the Auth flow, never here.
public record UserResponse
{
    public int UserId { get; init; }

    // MIGRATION: Multi-tenant discriminator (AAP 0.7.1) — every user belongs to a portal.
    public int PortalId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? DisplayName { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? FullName { get; init; }

    public bool IsSuperUser { get; init; }

    // MIGRATION: From legacy UserMembership.Approved (non-credential account-status flag).
    public bool IsApproved { get; init; }

    // MIGRATION: From legacy UserMembership.LockedOut (non-credential account-status flag).
    public bool LockedOut { get; init; }

    // MIGRATION: From legacy UserMembership.CreatedDate (non-credential lifecycle timestamp). VB Date -> DateTime?.
    public DateTime? CreatedDate { get; init; }

    // MIGRATION: From legacy UserMembership.LastLoginDate (non-credential lifecycle timestamp). VB Date -> DateTime?.
    public DateTime? LastLoginDate { get; init; }

    // MIGRATION: Legacy UserInfo.AffiliateID initialized to Null.NullInteger (-1) -> nullable int.
    public int? AffiliateId { get; init; }

    // MIGRATION: Flattened role-name list. The Domain User.UserRoles navigation (UserRole join entity) is NOT
    // exposed raw; the service/mapping layer projects it to role names (mirrors the legacy UserInfo.Roles String()).
    public IReadOnlyList<string> Roles { get; init; } = [];
}
