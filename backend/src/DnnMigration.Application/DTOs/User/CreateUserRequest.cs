// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) DnnMigration Project. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

// MIGRATION: This DTO is derived from Website/admin/Users/User.ascx.vb
// Original VB.NET CreateUser method (lines 209-238) and Validate method (lines 133-195)
// Property mappings from Library/Components/Users/UserInfo.vb and UserMembership.vb

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.User;

/// <summary>
/// Data Transfer Object for creating a new user in the system.
/// </summary>
/// <remarks>
/// <para>
/// This DTO captures all fields from the legacy User.ascx.vb form for user creation 
/// via POST /api/users endpoint. Fields are mapped from:
/// </para>
/// <list type="bullet">
///   <item><description>User.ascx.vb - CreateUser method and form controls</description></item>
///   <item><description>UserInfo.vb - User entity properties</description></item>
///   <item><description>UserMembership.vb - Membership-related properties</description></item>
/// </list>
/// <para>
/// MIGRATION: VB.NET UserInfo/UserMembership property assignments converted to C# record properties.
/// </para>
/// </remarks>
public record CreateUserRequest
{
    /// <summary>
    /// Gets or initializes the username for the new user account.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserEditor control binding to User.Username in User.ascx.vb.
    /// Maps to UserInfo.Username property (line 301 in UserInfo.vb).
    /// Username is marked as Required(True) in the original VB source.
    /// </remarks>
    /// <example>john.doe</example>
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, ErrorMessage = "Username cannot exceed 100 characters.")]
    public required string Username { get; init; }

    /// <summary>
    /// Gets or initializes the password for the new user account.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtPassword control in User.ascx.vb (lines 152, 160-161).
    /// Assigned to User.Membership.Password when not using random password generation.
    /// Required unless <see cref="GenerateRandomPassword"/> is set to true.
    /// </remarks>
    [Required(ErrorMessage = "Password is required when not generating a random password.")]
    [StringLength(128, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 128 characters.")]
    public required string Password { get; init; }

    /// <summary>
    /// Gets or initializes the password confirmation for validation purposes.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtConfirm control in User.ascx.vb (line 152).
    /// Used to verify password entry - validation checks txtPassword.Text != txtConfirm.Text
    /// which results in UserCreateStatus.PasswordMismatch error.
    /// </remarks>
    [StringLength(128, ErrorMessage = "Password confirmation cannot exceed 128 characters.")]
    public string? PasswordConfirm { get; init; }

    /// <summary>
    /// Gets or initializes the email address for the new user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserEditor control binding to User.Email in User.ascx.vb.
    /// Maps to UserInfo.Email property (lines 121-134 in UserInfo.vb).
    /// Has MaxLength(256), Required(True), and RegularExpressionValidator(glbEmailRegEx) in original.
    /// </remarks>
    /// <example>john.doe@example.com</example>
    [Required(ErrorMessage = "Email address is required.")]
    [StringLength(256, ErrorMessage = "Email address cannot exceed 256 characters.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public required string Email { get; init; }

    /// <summary>
    /// Gets or initializes the first name of the user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserEditor binding to User.FirstName in User.ascx.vb.
    /// Maps to UserInfo.FirstName property (lines 144-151 in UserInfo.vb).
    /// Has MaxLength(50), Required(True) attributes in original.
    /// Stored in UserProfile, accessed via Profile.FirstName.
    /// </remarks>
    /// <example>John</example>
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    public required string FirstName { get; init; }

    /// <summary>
    /// Gets or initializes the last name of the user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From UserEditor binding to User.LastName in User.ascx.vb.
    /// Maps to UserInfo.LastName property (lines 178-185 in UserInfo.vb).
    /// Has MaxLength(50), Required(True) attributes in original.
    /// Stored in UserProfile, accessed via Profile.LastName.
    /// </remarks>
    /// <example>Doe</example>
    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    public required string LastName { get; init; }

    /// <summary>
    /// Gets or initializes the display name for the user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: From UserEditor binding to User.DisplayName in User.ascx.vb.
    /// Maps to UserInfo.DisplayName property (lines 104-111 in UserInfo.vb).
    /// Has MaxLength(128) attribute in original.
    /// </para>
    /// <para>
    /// If not provided, the display name will be computed from the portal's 
    /// Security_DisplayNameFormat setting via UpdateDisplayName method (lines 117-123 in User.ascx.vb).
    /// Format can use tokens: [USERID], [FIRSTNAME], [LASTNAME], [USERNAME].
    /// </para>
    /// </remarks>
    /// <example>John Doe</example>
    [StringLength(128, ErrorMessage = "Display name cannot exceed 128 characters.")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or initializes whether the user is a super user (host-level administrator).
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to UserInfo.IsSuperUser property (lines 161-168 in UserInfo.vb).
    /// Super users have unrestricted access across all portals.
    /// Defaults to false for regular user creation.
    /// </remarks>
    public bool IsSuperUser { get; init; }

    /// <summary>
    /// Gets or initializes whether the user account is immediately authorized/approved.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkAuthorize checkbox in User.ascx.vb (line 223).
    /// Sets User.Membership.Approved property.
    /// When true, user can log in immediately; when false, requires admin approval.
    /// In registration scenarios, this is determined by PortalSettings.UserRegistration setting.
    /// </remarks>
    public bool IsAuthorized { get; init; }

    /// <summary>
    /// Gets or initializes whether to send a notification email to the new user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkNotify checkbox in User.ascx.vb (line 231).
    /// Sets args.Notify in UserCreatedEventArgs for email notification.
    /// When true, system sends welcome/account creation email to the user.
    /// </remarks>
    public bool Notify { get; init; }

    /// <summary>
    /// Gets or initializes whether to generate a random password for the user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkRandom checkbox in User.ascx.vb (lines 150, 162-165).
    /// When true, password is generated via UserController.GeneratePassword()
    /// and the Password/PasswordConfirm fields are ignored.
    /// </remarks>
    public bool GenerateRandomPassword { get; init; }

    /// <summary>
    /// Gets or initializes the security question for password recovery.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtQuestion control in User.ascx.vb (lines 169, 173).
    /// Assigned to User.Membership.PasswordQuestion.
    /// Required when MembershipProviderConfig.RequiresQuestionAndAnswer is true.
    /// Returns UserCreateStatus.InvalidQuestion if required but not provided.
    /// </remarks>
    /// <example>What is your mother's maiden name?</example>
    [StringLength(256, ErrorMessage = "Password question cannot exceed 256 characters.")]
    public string? PasswordQuestion { get; init; }

    /// <summary>
    /// Gets or initializes the answer to the security question.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtAnswer control in User.ascx.vb (lines 176, 180).
    /// Assigned to User.Membership.PasswordAnswer.
    /// Required when MembershipProviderConfig.RequiresQuestionAndAnswer is true.
    /// Returns UserCreateStatus.InvalidAnswer if required but not provided.
    /// </remarks>
    /// <example>Smith</example>
    [StringLength(128, ErrorMessage = "Password answer cannot exceed 128 characters.")]
    public string? PasswordAnswer { get; init; }
}
