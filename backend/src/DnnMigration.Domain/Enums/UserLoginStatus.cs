namespace DnnMigration.Domain.Enums;

/// <summary>
/// Represents the outcome of a user login attempt.
/// Ported 1:1 from the legacy DotNetNuke <c>4.9.0.85</c> VB.NET enum
/// <c>DotNetNuke.Security.Membership.UserLoginStatus</c>
/// (Library/Components/Users/Membership/UserLoginStatus.vb).
/// Consumed by the authentication flow when branching on a login result.
/// </summary>
/// <remarks>
/// MIGRATION: Member names are intentionally retained in the original
/// ALL-CAPS, underscore-separated form (rather than idiomatic C# PascalCase)
/// to preserve the public contract and the verbatim integer values (0..6),
/// so any persisted or compared values remain valid after migration.
/// </remarks>
public enum UserLoginStatus
{
    LOGIN_FAILURE = 0,
    LOGIN_SUCCESS = 1,
    LOGIN_SUPERUSER = 2,
    LOGIN_USERLOCKEDOUT = 3,
    LOGIN_USERNOTAPPROVED = 4,
    LOGIN_INSECUREADMINPASSWORD = 5,
    LOGIN_INSECUREHOSTPASSWORD = 6
}
