// MIGRATION: Replaces legacy FormsAuthentication cookie-based response with modern JWT response DTO
// Original: PortalSecurity.vb used FormsAuthentication.SetAuthCookie() for cookie-based auth
// Target: Stateless JWT Bearer tokens per BFF pattern (Section 0.3.2)

using System;
using System.Collections.Generic;

namespace DnnMigration.Application.DTOs.Auth;

/// <summary>
/// JWT authentication response DTO returned upon successful login or token refresh.
/// This replaces the legacy Forms Authentication cookie-based response with modern
/// stateless JWT tokens following the BFF (Backend-for-Frontend) pattern.
/// </summary>
/// <remarks>
/// <para>MIGRATION: This DTO replaces the following legacy authentication flow:</para>
/// <para>- FormsAuthentication.SetAuthCookie() from PortalSecurity.vb</para>
/// <para>- Cookie-based portalroles and portalaliasid storage</para>
/// <para>The new approach uses short-lived access tokens (60 min) with refresh tokens for renewal.</para>
/// </remarks>
/// <param name="AccessToken">The JWT bearer token for API authorization. Include in Authorization header as "Bearer {token}".</param>
/// <param name="RefreshToken">Token for obtaining a new access token when the current one expires.</param>
/// <param name="TokenType">The type of token issued. Always "Bearer" per OAuth 2.0/JWT conventions.</param>
/// <param name="ExpiresIn">Token lifetime in seconds. Default is 3600 (60 minutes) per BFF pattern specification.</param>
/// <param name="ExpiresAt">Absolute UTC timestamp when the access token expires.</param>
/// <param name="User">Embedded user information containing identity and authorization details.</param>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    DateTime ExpiresAt,
    AuthUserInfo User)
{
    /// <summary>
    /// Default token type for JWT Bearer authentication.
    /// </summary>
    public const string BearerTokenType = "Bearer";

    /// <summary>
    /// Default token expiration in seconds (60 minutes per BFF pattern specification).
    /// </summary>
    public const int DefaultExpiresInSeconds = 3600;

    /// <summary>
    /// Creates a new AuthResponse with the specified tokens and user information.
    /// </summary>
    /// <param name="accessToken">The JWT access token.</param>
    /// <param name="refreshToken">The refresh token for token renewal.</param>
    /// <param name="expiresIn">Token lifetime in seconds. Defaults to 3600 (60 minutes).</param>
    /// <param name="user">The authenticated user information.</param>
    /// <returns>A new AuthResponse instance with computed expiration timestamp.</returns>
    /// <remarks>
    /// This factory method automatically computes the ExpiresAt timestamp based on current UTC time
    /// and the provided expiresIn duration, ensuring consistent token expiration handling.
    /// </remarks>
    public static AuthResponse Create(
        string accessToken,
        string refreshToken,
        AuthUserInfo user,
        int expiresIn = DefaultExpiresInSeconds)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken));
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token cannot be null or empty.", nameof(refreshToken));
        }

        ArgumentNullException.ThrowIfNull(user);

        if (expiresIn <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresIn), "Token expiration must be greater than zero.");
        }

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            TokenType: BearerTokenType,
            ExpiresIn: expiresIn,
            ExpiresAt: DateTime.UtcNow.AddSeconds(expiresIn),
            User: user);
    }

    /// <summary>
    /// Creates a new AuthResponse with a pre-computed expiration timestamp.
    /// </summary>
    /// <param name="accessToken">The JWT access token.</param>
    /// <param name="refreshToken">The refresh token for token renewal.</param>
    /// <param name="user">The authenticated user information.</param>
    /// <param name="expiresAt">The absolute UTC expiration timestamp.</param>
    /// <returns>A new AuthResponse instance with the specified expiration timestamp.</returns>
    /// <remarks>
    /// Use this overload when the token expiration is already known or computed externally,
    /// such as when parsing an existing JWT token.
    /// </remarks>
    public static AuthResponse Create(
        string accessToken,
        string refreshToken,
        AuthUserInfo user,
        DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken));
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token cannot be null or empty.", nameof(refreshToken));
        }

        ArgumentNullException.ThrowIfNull(user);

        if (expiresAt <= DateTime.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Token expiration must be in the future.");
        }

        var expiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            TokenType: BearerTokenType,
            ExpiresIn: expiresIn,
            ExpiresAt: expiresAt,
            User: user);
    }
}

/// <summary>
/// Embedded user information included in the authentication response.
/// Contains essential identity and authorization details derived from the authenticated user.
/// </summary>
/// <remarks>
/// <para>MIGRATION: Maps from legacy UserInfo entity (Library/Components/Users/UserInfo.vb)</para>
/// <para>Properties mapped from VB.NET source:</para>
/// <list type="bullet">
/// <item><description>UserId from UserInfo.UserID (Integer → int)</description></item>
/// <item><description>Username from UserInfo.Username (String → string)</description></item>
/// <item><description>DisplayName from UserInfo.DisplayName (String → string?)</description></item>
/// <item><description>Email from UserInfo.Email (String → string?)</description></item>
/// <item><description>IsSuperUser from UserInfo.IsSuperUser (Boolean → bool)</description></item>
/// <item><description>Roles from UserInfo.Roles (String() → IReadOnlyList&lt;string&gt;)</description></item>
/// <item><description>PortalId from UserInfo.PortalID (Integer → int)</description></item>
/// </list>
/// </remarks>
/// <param name="UserId">Unique user identifier from the database.</param>
/// <param name="Username">The user's login username. Required and non-nullable.</param>
/// <param name="DisplayName">The user's display name. May be null if not set.</param>
/// <param name="Email">The user's email address. May be null if not provided.</param>
/// <param name="IsSuperUser">Indicates whether the user has superuser/admin privileges.</param>
/// <param name="Roles">Collection of role names assigned to the user. Empty collection if no roles assigned.</param>
/// <param name="PortalId">The portal context identifier for multi-tenant isolation.</param>
public record AuthUserInfo(
    int UserId,
    string Username,
    string? DisplayName,
    string? Email,
    bool IsSuperUser,
    IReadOnlyList<string> Roles,
    int PortalId)
{
    /// <summary>
    /// Creates a new AuthUserInfo with the specified user details.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <param name="username">The user's login username.</param>
    /// <param name="displayName">Optional display name.</param>
    /// <param name="email">Optional email address.</param>
    /// <param name="isSuperUser">Whether the user has superuser privileges.</param>
    /// <param name="roles">Collection of role names. If null, an empty list is used.</param>
    /// <param name="portalId">The portal context identifier.</param>
    /// <returns>A new AuthUserInfo instance.</returns>
    /// <remarks>
    /// MIGRATION: This factory method provides a convenient way to create AuthUserInfo
    /// from domain User entity, handling null roles array from legacy UserInfo.Roles property.
    /// </remarks>
    public static AuthUserInfo Create(
        int userId,
        string username,
        string? displayName,
        string? email,
        bool isSuperUser,
        IEnumerable<string>? roles,
        int portalId)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        }

        // Convert roles to IReadOnlyList, ensuring non-null immutable collection
        // MIGRATION: Legacy UserInfo.Roles was a String() array that could be Nothing
        IReadOnlyList<string> rolesList = roles is not null
            ? new List<string>(roles).AsReadOnly()
            : Array.Empty<string>();

        return new AuthUserInfo(
            UserId: userId,
            Username: username,
            DisplayName: displayName,
            Email: email,
            IsSuperUser: isSuperUser,
            Roles: rolesList,
            PortalId: portalId);
    }

    /// <summary>
    /// Checks if the user is in the specified role.
    /// </summary>
    /// <param name="roleName">The role name to check.</param>
    /// <returns>True if the user has the specified role or is a superuser; otherwise, false.</returns>
    /// <remarks>
    /// MIGRATION: Mirrors the logic from UserInfo.IsInRole() method in VB.NET source.
    /// Superusers are implicitly granted all roles per legacy behavior.
    /// </remarks>
    public bool IsInRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        // MIGRATION: SuperUsers have access to all roles per legacy UserInfo.IsInRole behavior
        if (IsSuperUser)
        {
            return true;
        }

        foreach (var role in Roles)
        {
            if (string.Equals(role, roleName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
