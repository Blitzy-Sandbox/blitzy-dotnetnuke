// <copyright file="UserRegistrationType.cs" company="DNN Migration Project">
// Copyright (c) DNN Migration Project. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

// MIGRATION: Converted from VB.NET PortalRegistrationType enum in Library/Components/Shared/Globals.vb
// MIGRATION: Original enum name: PortalRegistrationType (renamed to UserRegistrationType for clarity)
// MIGRATION: Values renamed: NoRegistration→None, PrivateRegistration→Private, PublicRegistration→Public, VerifiedRegistration→Verified
// MIGRATION: Explicit integer values preserved (0, 1, 2, 3) for database compatibility with existing DNN schema

namespace DnnMigration.Domain.Enums;

/// <summary>
/// Defines the user registration modes available for portal configuration.
/// </summary>
/// <remarks>
/// <para>
/// This enumeration controls how user registration is handled for a portal.
/// The integer values are explicitly defined to maintain database compatibility
/// with the existing DotNetNuke schema where these values are stored in the
/// Portals table's UserRegistration column.
/// </para>
/// <para>
/// Migrated from the legacy <c>PortalRegistrationType</c> enum in 
/// <c>DotNetNuke.Common.Globals</c> (Library/Components/Shared/Globals.vb).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Example usage in a Portal entity or service:
/// var portal = new Portal
/// {
///     UserRegistration = UserRegistrationType.Public
/// };
/// 
/// // Check registration type
/// if (portal.UserRegistration == UserRegistrationType.Verified)
/// {
///     // Require email verification
/// }
/// </code>
/// </example>
[Serializable]
public enum UserRegistrationType
{
    /// <summary>
    /// User registration is disabled for the portal.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="None"/>, new users cannot register on the portal.
    /// Only administrators can create new user accounts.
    /// Database value: 0 (maps to NoRegistration in legacy DNN schema).
    /// </remarks>
    None = 0,

    /// <summary>
    /// User registration requires administrator approval.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="Private"/>, new users can submit registration requests,
    /// but their accounts remain inactive until approved by a portal administrator.
    /// This mode is useful for private or restricted-access portals.
    /// Database value: 1 (maps to PrivateRegistration in legacy DNN schema).
    /// </remarks>
    Private = 1,

    /// <summary>
    /// User registration is open to the public without restrictions.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="Public"/>, new users can register and immediately
    /// gain access to the portal with the default registered user role.
    /// No administrator approval or email verification is required.
    /// Database value: 2 (maps to PublicRegistration in legacy DNN schema).
    /// </remarks>
    Public = 2,

    /// <summary>
    /// User registration requires email verification.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="Verified"/>, new users must verify their email address
    /// before their account becomes active. A verification email is sent during
    /// registration, and the user must click the verification link to activate
    /// their account. This mode helps prevent spam registrations and ensures
    /// valid email addresses.
    /// Database value: 3 (maps to VerifiedRegistration in legacy DNN schema).
    /// </remarks>
    Verified = 3
}
