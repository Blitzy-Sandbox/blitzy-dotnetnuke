// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Users.UserInfo → C# 12 User entity
// Source: Library/Components/Users/UserInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted Private fields (_UserID, _Username, etc.) to C# auto-properties
// - Removed IPropertyAccess implementation (token replacement)
// - Removed Browsable, SortOrder, Required attributes (handled by FluentValidation)
// - Removed progressive hydration logic (handled by EF Core lazy loading)
// - Integrated key membership properties directly (Email, Password sync removed)
// - Roles property replaced with UserRoles navigation collection
// - Added navigation properties for Portal, Profile, and UserRoles
// - Applied nullable reference types
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a user within the DNN system.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the Users table and represents an individual user account.
/// Each user belongs to a specific portal and can be assigned to multiple roles
/// through the <see cref="UserRole"/> association entity.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserInfo.
/// Progressive hydration logic removed - EF Core lazy loading handles on-demand loading.
/// </para>
/// </remarks>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    /// <value>The primary key of the user record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _UserID field.
    /// </remarks>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets the portal identifier that this user belongs to.
    /// </summary>
    /// <value>The foreign key to the Portal entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _PortalID field.
    /// </remarks>
    public int PortalId { get; set; }

    /// <summary>
    /// Gets or sets the username for the user.
    /// </summary>
    /// <value>The unique username for authentication.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Username field.
    /// </remarks>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for the user.
    /// </summary>
    /// <value>The name displayed in the UI.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _DisplayName field.
    /// </remarks>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the email address for the user.
    /// </summary>
    /// <value>The email address used for communication and authentication.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Email field.
    /// Originally also synced to Membership.Email.
    /// </remarks>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is a super user (host admin).
    /// </summary>
    /// <value><c>true</c> if the user is a super user; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IsSuperUser field.
    /// Super users have access to all portals and host-level settings.
    /// </remarks>
    public bool IsSuperUser { get; set; }

    /// <summary>
    /// Gets or sets the affiliate identifier for the user.
    /// </summary>
    /// <value>The affiliate ID for tracking referrals. May be null if not an affiliate.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _AffiliateID field.
    /// </remarks>
    public int? AffiliateId { get; set; }

    /// <summary>
    /// Gets or sets the first name of the user.
    /// </summary>
    /// <value>The user's first name.</value>
    /// <remarks>
    /// MIGRATION: Originally accessed via Profile.FirstName.
    /// Now stored directly for simpler querying.
    /// </remarks>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name of the user.
    /// </summary>
    /// <value>The user's last name.</value>
    /// <remarks>
    /// MIGRATION: Originally accessed via Profile.LastName.
    /// Now stored directly for simpler querying.
    /// </remarks>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user account is approved.
    /// </summary>
    /// <value><c>true</c> if the user is approved; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Originally part of UserMembership object.
    /// </remarks>
    public bool IsApproved { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user account is locked out.
    /// </summary>
    /// <value><c>true</c> if the user is locked out; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Originally part of UserMembership object.
    /// </remarks>
    public bool IsLockedOut { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the user was created.
    /// </summary>
    /// <value>The creation date of the user account.</value>
    /// <remarks>
    /// MIGRATION: Originally part of UserMembership object.
    /// </remarks>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the user's last login.
    /// </summary>
    /// <value>The date of the last successful login. May be null if never logged in.</value>
    /// <remarks>
    /// MIGRATION: Originally part of UserMembership object.
    /// </remarks>
    public DateTime? LastLoginDate { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the user's last activity.
    /// </summary>
    /// <value>The date of the last activity. May be null.</value>
    /// <remarks>
    /// MIGRATION: Originally part of UserMembership object.
    /// </remarks>
    public DateTime? LastActivityDate { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the password was last changed.
    /// </summary>
    /// <value>The date of the last password change. May be null.</value>
    /// <remarks>
    /// MIGRATION: Originally part of UserMembership object.
    /// </remarks>
    public DateTime? LastPasswordChangeDate { get; set; }

    /// <summary>
    /// Gets or sets the BCrypt hashed password for the user.
    /// </summary>
    /// <value>The BCrypt password hash string.</value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Originally part of UserMembership object as Password property.
    /// The legacy system used DES encryption via PortalSecurity.Encrypt.
    /// This migration uses BCrypt for secure one-way password hashing.
    /// </para>
    /// <para>
    /// Format: $2a$[workFactor]$[22-char-salt][31-char-hash]
    /// </para>
    /// </remarks>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user must change their password on next login.
    /// </summary>
    /// <value><c>true</c> if the user must change password; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Originally part of UserMembership.UpdatePassword logic.
    /// Set to true when admin resets password or when password expires.
    /// </remarks>
    public bool ForcePasswordChange { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the user was last locked out.
    /// </summary>
    /// <value>The date of the last lockout. May be null if never locked out.</value>
    /// <remarks>
    /// MIGRATION: Originally part of UserMembership object.
    /// </remarks>
    public DateTime? LastLockoutDate { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the user account was last updated.
    /// </summary>
    /// <value>The last update date.</value>
    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has been deleted (soft delete).
    /// </summary>
    /// <value><c>true</c> if the user is deleted; otherwise, <c>false</c>.</value>
    public bool IsDeleted { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the portal that this user belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Portal"/> entity.</value>
    public virtual Portal? Portal { get; set; }

    /// <summary>
    /// Gets or sets the profile for this user.
    /// </summary>
    /// <value>Navigation property to the <see cref="UserProfile"/> entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Profile field with progressive hydration.
    /// </remarks>
    public virtual UserProfile? Profile { get; set; }

    /// <summary>
    /// Gets or sets the collection of user-role assignments for this user.
    /// </summary>
    /// <value>A collection of <see cref="UserRole"/> entities representing roles assigned to this user.</value>
    /// <remarks>
    /// MIGRATION: Replaces the Roles string array property with proper many-to-many relationship.
    /// </remarks>
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    /// <summary>
    /// Gets the full name of the user.
    /// </summary>
    /// <value>A computed full name combining first and last names.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET deprecated FullName property.
    /// </remarks>
    public string FullName => string.IsNullOrEmpty(FirstName) && string.IsNullOrEmpty(LastName)
        ? DisplayName ?? Username
        : $"{FirstName} {LastName}".Trim();
}
