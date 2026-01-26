// <copyright file="LoginRequest.cs" company="DnnMigration">
// Copyright (c) DnnMigration. All rights reserved.
// Licensed under the MIT License.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.Auth;

/// <summary>
/// Represents a JWT authentication request DTO containing user credentials for login.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This DTO replaces the legacy FormsAuthentication login model with a modern JWT request DTO.
/// Original DNN 4.x used Forms Authentication with cookies via <c>UserController.UserLogin</c> method
/// which accepted Username, Password, and CreatePersistentCookie parameters.
/// </para>
/// <para>
/// The <see cref="UsernameOrEmail"/> property supports login via either username or email address,
/// preserving the original DNN behavior where <c>UserController.ValidateUser</c> supported both methods.
/// </para>
/// <para>
/// The <see cref="RememberMe"/> property maps to the legacy CreatePersistentCookie parameter,
/// which controlled whether the authentication cookie persisted across browser sessions.
/// In the JWT model, this affects the token lifetime (extended duration for "remember me" scenarios).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var request = new LoginRequest
/// {
///     UsernameOrEmail = "admin@example.com",
///     Password = "SecurePassword123!",
///     RememberMe = true
/// };
/// </code>
/// </example>
public record LoginRequest
{
    /// <summary>
    /// Gets or sets the username or email address for authentication.
    /// </summary>
    /// <value>
    /// A non-null string containing either the user's username or email address.
    /// </value>
    /// <remarks>
    /// MIGRATION: Original DNN supported login via both username and email through
    /// <c>UserController.ValidateUser</c> and <c>GetUserByUserName</c>/<c>GetUsersByEmail</c> methods.
    /// The authentication service should attempt lookup by username first, then by email if not found.
    /// Maximum length of 256 characters matches the legacy DNN Email field maximum length.
    /// </remarks>
    [Required(ErrorMessage = "Username or email is required.")]
    [StringLength(256, ErrorMessage = "Username or email cannot exceed 256 characters.")]
    public required string UsernameOrEmail { get; init; }

    /// <summary>
    /// Gets or sets the user's password credential.
    /// </summary>
    /// <value>
    /// A non-null string containing the user's password.
    /// </value>
    /// <remarks>
    /// MIGRATION: Maps to the Password parameter in legacy <c>UserController.UserLogin</c> method.
    /// The password is validated against the stored hash using the configured password hasher.
    /// Maximum length of 128 characters provides adequate space for strong passwords while
    /// preventing excessive input that could impact performance.
    /// </remarks>
    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
    [DataType(DataType.Password)]
    public required string Password { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the login session should persist.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable extended token lifetime for persistent login;
    /// <c>false</c> to use standard token lifetime. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// MIGRATION: Maps to the <c>CreatePersistentCookie</c> parameter in legacy
    /// <c>UserController.UserLogin</c> method. In the original DNN implementation,
    /// when <c>CreatePersistentCookie</c> was true and <c>PersistentCookieTimeout</c>
    /// was configured, a longer-lived FormsAuthenticationTicket was created.
    /// In the JWT model, this flag signals the authentication service to issue
    /// tokens with an extended lifetime (e.g., 30 days vs. 60 minutes).
    /// </remarks>
    public bool RememberMe { get; init; } = false;
}
