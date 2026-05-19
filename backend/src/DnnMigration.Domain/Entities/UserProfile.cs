// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Users.UserProfile → C# 12 UserProfile entity
// Source: Library/Components/Users/Profile/UserProfile.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Replaced extensible ProfilePropertyDefinitionCollection with direct properties
// - Removed IsDirty, ObjectHydrated tracking (handled by EF Core change tracking)
// - Removed GetPropertyValue/SetProfileProperty helper methods
// - Applied nullable reference types for all optional string fields
// - Added navigation property back to User
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a user's profile information containing personal and contact details.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the UserProfile table and stores extended user information
/// such as address, phone numbers, and preferences. Each profile belongs to a
/// single <see cref="User"/> entity.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserProfile.
/// The original extensible property collection has been replaced with fixed properties
/// for the commonly-used profile fields. Custom profile fields should be handled
/// by a separate extension mechanism if needed.
/// </para>
/// </remarks>
public class UserProfile
{
    /// <summary>
    /// Gets or sets the unique identifier for the user profile.
    /// </summary>
    /// <value>The primary key of the user profile record.</value>
    public int ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier that this profile belongs to.
    /// </summary>
    /// <value>The foreign key to the User entity.</value>
    public int UserId { get; set; }

    // Name properties

    /// <summary>
    /// Gets or sets the name prefix (e.g., Mr., Mrs., Dr.).
    /// </summary>
    /// <value>The honorific or title prefix. May be null.</value>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    /// <value>The user's first name. May be null.</value>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the middle name.
    /// </summary>
    /// <value>The user's middle name. May be null.</value>
    public string? MiddleName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    /// <value>The user's last name. May be null.</value>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the name suffix (e.g., Jr., Sr., III).
    /// </summary>
    /// <value>The name suffix. May be null.</value>
    public string? Suffix { get; set; }

    /// <summary>
    /// Gets the full name of the user by combining first and last names.
    /// </summary>
    /// <value>A computed full name combining first and last names, trimmed of whitespace.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET ReadOnly Property FullName() which returned FirstName &amp; " " &amp; LastName.
    /// Returns an empty string if both first and last names are null or empty.
    /// </remarks>
    public string FullName
    {
        get
        {
            var firstName = FirstName ?? string.Empty;
            var lastName = LastName ?? string.Empty;
            return $"{firstName} {lastName}".Trim();
        }
    }

    // Address properties

    /// <summary>
    /// Gets or sets the unit or apartment number.
    /// </summary>
    /// <value>The unit/apartment portion of the address. May be null.</value>
    public string? Unit { get; set; }

    /// <summary>
    /// Gets or sets the street address.
    /// </summary>
    /// <value>The street address. May be null.</value>
    public string? Street { get; set; }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    /// <value>The city name. May be null.</value>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the region, state, or province.
    /// </summary>
    /// <value>The region/state/province. May be null.</value>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the country.
    /// </summary>
    /// <value>The country name or code. May be null.</value>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the postal code or ZIP code.
    /// </summary>
    /// <value>The postal/ZIP code. May be null.</value>
    public string? PostalCode { get; set; }

    // Phone contact properties

    /// <summary>
    /// Gets or sets the telephone number.
    /// </summary>
    /// <value>The primary telephone number. May be null.</value>
    public string? Telephone { get; set; }

    /// <summary>
    /// Gets or sets the cell/mobile phone number.
    /// </summary>
    /// <value>The mobile phone number. May be null.</value>
    public string? Cell { get; set; }

    /// <summary>
    /// Gets or sets the fax number.
    /// </summary>
    /// <value>The fax number. May be null.</value>
    public string? Fax { get; set; }

    // Online contact properties

    /// <summary>
    /// Gets or sets the website URL.
    /// </summary>
    /// <value>The user's website URL. May be null.</value>
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the instant messaging handle.
    /// </summary>
    /// <value>The IM handle/username. May be null.</value>
    public string? IM { get; set; }

    // Preferences

    /// <summary>
    /// Gets or sets the time zone offset in minutes.
    /// </summary>
    /// <value>The user's preferred time zone offset from UTC.</value>
    public int? TimeZone { get; set; }

    /// <summary>
    /// Gets or sets the preferred locale/language code.
    /// </summary>
    /// <value>The preferred locale (e.g., "en-US"). May be null.</value>
    public string? PreferredLocale { get; set; }

    /// <summary>
    /// Gets or sets the biography or about text.
    /// </summary>
    /// <value>A short biography or description. May be null.</value>
    public string? Biography { get; set; }

    /// <summary>
    /// Gets or sets the profile photo/avatar path.
    /// </summary>
    /// <value>The relative path to the profile photo. May be null.</value>
    public string? Photo { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the user that this profile belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="User"/> entity.</value>
    public virtual User? User { get; set; }
}
