namespace DnnMigration.Application.DTOs;

/// <summary>
/// Read-only projection of a user's membership/account state.
/// MIGRATION: mapped from the legacy DotNetNuke UserMembership entity
/// (Library/Components/Users/Membership/UserMembership.vb). Secrets
/// (Password/PasswordAnswer/PasswordQuestion) are intentionally excluded.
/// </summary>
public record MembershipDto
{
    public bool Approved { get; init; }
    public bool LockedOut { get; init; }
    public bool IsOnLine { get; init; }
    public bool UpdatePassword { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime LastLoginDate { get; init; }
    public DateTime LastActivityDate { get; init; }
    public DateTime LastLockoutDate { get; init; }
    public DateTime LastPasswordChangeDate { get; init; }
}
