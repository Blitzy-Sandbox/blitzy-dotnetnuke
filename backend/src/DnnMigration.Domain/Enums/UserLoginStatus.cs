// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 rewrite of DotNetNuke
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Converted from DotNetNuke.Security.Membership.UserLoginStatus (VB.NET)
// Source: Library/Components/Users/Membership/UserLoginStatus.vb
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Enums;

/// <summary>
/// Defines the possible status codes returned from user login validation operations.
/// Used by the authentication service to indicate the result of login attempts.
/// </summary>
/// <remarks>
/// MIGRATION: Converted from VB.NET UserLoginStatus enum in DotNetNuke.Security.Membership namespace.
/// Explicit integer values are preserved for backward compatibility with legacy security logic
/// and database stored procedures that may rely on these specific values.
/// </remarks>
[Serializable]
public enum UserLoginStatus
{
    /// <summary>
    /// Generic login failure. Returned when authentication fails due to invalid credentials
    /// or other unspecified reasons.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Converted from LOGIN_FAILURE = 0.
    /// This is the default failure status when no specific failure reason applies.
    /// </remarks>
    LoginFailure = 0,

    /// <summary>
    /// Successful login for a standard (non-super) user.
    /// Indicates the user has been authenticated successfully.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Converted from LOGIN_SUCCESS = 1.
    /// User credentials are valid and the account is in good standing.
    /// </remarks>
    LoginSuccess = 1,

    /// <summary>
    /// Successful login for a super user (host administrator).
    /// Indicates the user has been authenticated with elevated privileges.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Converted from LOGIN_SUPERUSER = 2.
    /// Super users have system-wide administrative access across all portals.
    /// </remarks>
    LoginSuperUser = 2,

    /// <summary>
    /// Login denied because the user account is locked out.
    /// Typically occurs after too many failed login attempts.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Converted from LOGIN_USERLOCKEDOUT = 3.
    /// The user must wait for the lockout period to expire or contact an administrator.
    /// </remarks>
    LoginUserLockedOut = 3,

    /// <summary>
    /// Login denied because the user account has not been approved.
    /// Occurs when user registration requires administrator approval.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Converted from LOGIN_USERNOTAPPROVED = 4.
    /// The administrator must approve the account before the user can log in.
    /// </remarks>
    LoginUserNotApproved = 4,

    /// <summary>
    /// Login succeeded but the administrator is using an insecure password.
    /// A warning status indicating the admin should change their password.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Converted from LOGIN_INSECUREADMINPASSWORD = 5.
    /// The system should prompt the administrator to update their password.
    /// </remarks>
    LoginInsecureAdminPassword = 5,

    /// <summary>
    /// Login succeeded but the host user is using an insecure password.
    /// A warning status indicating the host should change their password.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Converted from LOGIN_INSECUREHOSTPASSWORD = 6.
    /// The system should prompt the host user to update their password.
    /// </remarks>
    LoginInsecureHostPassword = 6
}
