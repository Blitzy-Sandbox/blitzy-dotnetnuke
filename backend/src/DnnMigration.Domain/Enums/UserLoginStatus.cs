namespace DnnMigration.Domain.Enums;

/// <summary>
/// Represents the outcome of a user login attempt.
/// Ported verbatim from the legacy DotNetNuke 4.9.0.85 enum
/// <c>DotNetNuke.Security.Membership.UserLoginStatus</c>
/// (Library/Components/Users/Membership/UserLoginStatus.vb). Consumed by the
/// authentication flow (e.g., the Application <c>AuthService</c> login path) to
/// branch on the result of a credential validation.
/// </summary>
// MIGRATION: Member names are intentionally retained in the legacy ALL-CAPS,
// underscore-separated form (e.g., LOGIN_FAILURE) rather than PascalCase to preserve
// the public-contract semantics and keep any persisted/compared integer values valid
// (AAP section 0.7.1, Minimal Change Clause). The explicit values 0..6 mirror the VB source 1:1.
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
