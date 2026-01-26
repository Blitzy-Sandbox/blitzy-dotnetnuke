// -----------------------------------------------------------------------------
// <copyright file="UserDto.cs" company="DNN Migration Project">
//     Copyright (c) DNN Migration Project. All rights reserved.
//     Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------------
// MIGRATION: Converted from VB.NET UserInfo.vb (DotNetNuke.Entities.Users namespace)
// Original file: Library/Components/Users/UserInfo.vb
// This DTO represents user data for API responses, replacing the legacy UserInfo entity
// with a simplified, immutable record type suitable for JSON serialization.
// -----------------------------------------------------------------------------

namespace DnnMigration.Application.DTOs.User;

/// <summary>
/// Data Transfer Object representing a user entity in API responses.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This record type is converted from the VB.NET UserInfo class which was
/// the primary business layer model for Users in DotNetNuke 4.x.
/// </para>
/// <para>
/// Key changes from original:
/// - Converted from VB.NET class to C# 12 record type for immutability
/// - Removed IPropertyAccess implementation (not needed for REST API)
/// - Removed Browsable/SortOrder/IsReadOnly attributes (using DataAnnotations instead)
/// - Flattened UserMembership composition into direct properties
/// - Converted Null.NullInteger patterns to nullable types
/// - Array Roles converted to IReadOnlyList for immutability
/// </para>
/// </remarks>
/// <example>
/// Sample JSON response:
/// <code>
/// {
///   "userId": 1,
///   "username": "admin",
///   "displayName": "Administrator",
///   "firstName": "Admin",
///   "lastName": "User",
///   "email": "admin@example.com",
///   "portalId": 0,
///   "isSuperUser": true,
///   "affiliateId": null,
///   "roles": ["Administrators", "Registered Users"],
///   "isApproved": true,
///   "isLockedOut": false,
///   "isOnline": true,
///   "createdDate": "2024-01-01T00:00:00Z",
///   "lastLoginDate": "2024-01-15T10:30:00Z",
///   "lastActivityDate": "2024-01-15T11:00:00Z"
/// }
/// </code>
/// </example>
public record UserDto
{
    /// <summary>
    /// Gets the unique identifier for the user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET _UserID (Integer) field, line 47 of UserInfo.vb.
    /// Primary key in the Users table.
    /// </remarks>
    public int UserId { get; init; }

    /// <summary>
    /// Gets the username for authentication.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET _Username (String) field, line 48 of UserInfo.vb.
    /// Marked as Required(True) and IsReadOnly(True) in original - cannot be changed after creation.
    /// Maximum length enforced at 100 characters in database.
    /// </remarks>
    public required string Username { get; init; }

    /// <summary>
    /// Gets the display name shown in the UI.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET _DisplayName (String) field, line 49 of UserInfo.vb.
    /// Marked with MaxLength(128) in original. Can be formatted using UpdateDisplayName method
    /// with tokens like [USERID], [FIRSTNAME], [LASTNAME], [USERNAME].
    /// </remarks>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the user's first name from their profile.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET Profile.FirstName property, lines 144-150 of UserInfo.vb.
    /// Stored in UserProfile object, marked with MaxLength(50) in original.
    /// </remarks>
    public string? FirstName { get; init; }

    /// <summary>
    /// Gets the user's last name from their profile.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET Profile.LastName property, lines 178-185 of UserInfo.vb.
    /// Stored in UserProfile object, marked with MaxLength(50) in original.
    /// </remarks>
    public string? LastName { get; init; }

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET _Email (String) field, line 51 of UserInfo.vb.
    /// Marked with MaxLength(256) and RegularExpressionValidator(glbEmailRegEx) in original.
    /// Also synced to Membership.Email property.
    /// </remarks>
    public required string Email { get; init; }

    /// <summary>
    /// Gets the portal/site identifier the user belongs to.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET _PortalID (Integer) field, line 52 of UserInfo.vb.
    /// Determines multi-tenant portal context. A value of -1 indicates host user
    /// (converted from Null.NullInteger in original).
    /// </remarks>
    public int PortalId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user is a super user (host/admin).
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET _IsSuperUser (Boolean) field, line 53 of UserInfo.vb.
    /// Super users have administrative access across all portals.
    /// </remarks>
    public bool IsSuperUser { get; init; }

    /// <summary>
    /// Gets the affiliate identifier for tracking referrals.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET _AffiliateID (Integer) field, line 54 of UserInfo.vb.
    /// Original uses Null.NullInteger (-1) for no affiliate; converted to nullable int.
    /// </remarks>
    public int? AffiliateId { get; init; }

    /// <summary>
    /// Gets the list of role names assigned to this user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET _Roles As String() field, line 57 of UserInfo.vb.
    /// Converted from String array to IReadOnlyList for immutability.
    /// Roles are auto-hydrated on demand in original via RoleController.GetRolesByUser.
    /// </remarks>
    public IReadOnlyList<string>? Roles { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user account is approved.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET Membership.Approved property (UserMembership.vb, line 83).
    /// Defaults to true. Controls whether user can log in.
    /// </remarks>
    public bool IsApproved { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user account is locked out.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET Membership.LockedOut property (UserMembership.vb, line 223).
    /// Defaults to false. Set to true after failed login attempts exceed threshold.
    /// Can be unlocked by administrators.
    /// </remarks>
    public bool IsLockedOut { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user is currently online.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET Membership.IsOnLine property (UserMembership.vb, line 123).
    /// Determined by LastActivityDate being within the configured online window.
    /// </remarks>
    public bool IsOnline { get; init; }

    /// <summary>
    /// Gets the date and time when the user account was created.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET Membership.CreatedDate property (UserMembership.vb, line 103).
    /// Marked as IsReadOnly(True) in original. Null if not yet hydrated.
    /// </remarks>
    public DateTime? CreatedDate { get; init; }

    /// <summary>
    /// Gets the date and time of the user's last successful login.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET Membership.LastLoginDate property (UserMembership.vb, line 183).
    /// Marked as IsReadOnly(True) in original. Updated on successful authentication.
    /// </remarks>
    public DateTime? LastLoginDate { get; init; }

    /// <summary>
    /// Gets the date and time of the user's last activity.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From VB.NET Membership.LastActivityDate property (UserMembership.vb, line 143).
    /// Marked as IsReadOnly(True) in original. Used to determine online status.
    /// </remarks>
    public DateTime? LastActivityDate { get; init; }
}
