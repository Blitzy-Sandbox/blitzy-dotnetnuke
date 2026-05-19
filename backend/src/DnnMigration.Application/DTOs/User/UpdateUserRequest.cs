// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: This DTO is derived from the following legacy VB.NET source files:
// - Website/admin/Users/User.ascx.vb (cmdUpdate_Click handler, UpdateDisplayName method)
// - Website/admin/Users/ManageUsers.ascx.vb (UserController.UpdateUser call pattern)
// - Website/admin/Users/Membership.ascx.vb (authorize, unauthorize, unlock, password update operations)
// - Library/Components/Users/UserInfo.vb (entity property definitions and validation attributes)
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.User;

/// <summary>
/// Data Transfer Object for updating an existing user.
/// All properties are nullable to support partial updates (PATCH semantics).
/// Only provided fields will be updated; null values indicate no change.
/// </summary>
/// <remarks>
/// MIGRATION: This DTO captures update fields from User.ascx.vb and ManageUsers.ascx.vb
/// for user modification via PUT /api/users/{id} endpoint.
/// Unlike CreateUserRequest, Username cannot be changed after creation (per DNN pattern).
/// 
/// The following update operations are supported:
/// - Basic user information: DisplayName, FirstName, LastName, Email
/// - Administrative flags: IsSuperUser (admin-only)
/// - Membership status: IsApproved, IsLockedOut (unlock capability), ForcePasswordUpdate
/// - Affiliate association: AffiliateId
/// </remarks>
public record UpdateUserRequest
{
    /// <summary>
    /// Gets or sets the display name for the user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserEditor binding to User.DisplayName (UpdateDisplayName method lines 117-123 in User.ascx.vb).
    /// The display name may be automatically formatted based on portal settings (Security_DisplayNameFormat).
    /// Maximum length is 128 characters per legacy MaxLength(128) attribute in UserInfo.vb.
    /// </remarks>
    /// <example>John Smith</example>
    [StringLength(128, ErrorMessage = "Display name cannot exceed 128 characters.")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the first name of the user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserEditor binding to User.FirstName (profile update).
    /// This value is stored in the user's profile (UserProfile.FirstName).
    /// Maximum length is 50 characters per legacy MaxLength(50) attribute in UserInfo.vb.
    /// </remarks>
    /// <example>John</example>
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    public string? FirstName { get; init; }

    /// <summary>
    /// Gets or sets the last name of the user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserEditor binding to User.LastName (profile update).
    /// This value is stored in the user's profile (UserProfile.LastName).
    /// Maximum length is 50 characters per legacy MaxLength(50) attribute in UserInfo.vb.
    /// </remarks>
    /// <example>Smith</example>
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    public string? LastName { get; init; }

    /// <summary>
    /// Gets or sets the email address of the user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserEditor binding to User.Email with validation.
    /// Legacy system used RegularExpressionValidator(glbEmailRegEx) for email validation.
    /// Maximum length is 256 characters per legacy MaxLength(256) attribute in UserInfo.vb.
    /// When provided, must be a valid email format.
    /// </remarks>
    /// <example>john.smith@example.com</example>
    [StringLength(256, ErrorMessage = "Email address cannot exceed 256 characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string? Email { get; init; }

    /// <summary>
    /// Gets or sets whether the user has super user (host) privileges.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Admin-only operation to update super user status.
    /// From UserInfo.vb _IsSuperUser property (line 161-168).
    /// This should only be modifiable by existing super users.
    /// Setting to true grants host-level access across all portals.
    /// </remarks>
    public bool? IsSuperUser { get; init; }

    /// <summary>
    /// Gets or sets whether the user account is approved/authorized.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From Membership.ascx.vb chkAuthorize checkbox and cmdAuthorize_Click/cmdUnAuthorize_Click handlers.
    /// - cmdAuthorize_Click (line 194-206): Sets User.Membership.Approved = True, calls UserController.UpdateUser
    /// - cmdUnAuthorize_Click (line 238-250): Sets User.Membership.Approved = False, calls UserController.UpdateUser
    /// When set to true, the user is authorized to access the portal.
    /// When set to false, the user is unauthorized and cannot log in.
    /// </remarks>
    public bool? IsApproved { get; init; }

    /// <summary>
    /// Gets or sets whether the user account is locked out.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From Membership.ascx.vb cmdUnLock_Click handler (lines 260-269).
    /// Original code: UserController.UnLockUser(User) then User.Membership.LockedOut = False
    /// 
    /// This property only supports setting to false to unlock a locked account.
    /// Accounts become locked due to too many failed login attempts.
    /// Setting this to true has no effect (accounts cannot be manually locked).
    /// To unlock a user, set this value to false.
    /// </remarks>
    public bool? IsLockedOut { get; init; }

    /// <summary>
    /// Gets or sets whether the user must change their password on next login.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From Membership.ascx.vb cmdPassword_Click handler (lines 216-228).
    /// Original code: User.Membership.UpdatePassword = True, then calls UserController.UpdateUser
    /// Also mapped from chkUpdatePassword checkbox in legacy UI.
    /// 
    /// When set to true, the user will be required to change their password
    /// upon their next successful authentication.
    /// </remarks>
    public bool? ForcePasswordUpdate { get; init; }

    /// <summary>
    /// Gets or sets the affiliate ID associated with this user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserInfo.vb _AffiliateID property (lines 87-94).
    /// Original type was Integer with Null.NullInteger for no affiliate.
    /// In the migrated system, null represents no affiliate association.
    /// Used for tracking referral/affiliate relationships.
    /// </remarks>
    public int? AffiliateId { get; init; }
}
