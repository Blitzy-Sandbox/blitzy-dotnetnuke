namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload to update an existing user's identity and profile.
/// Bound from the body of <c>PUT /api/users/{id}</c>; the user id is taken from
/// the route, not from this payload. Validated by <c>Validators/UserValidator</c>.
/// </summary>
/// <remarks>
/// MIGRATION: identity fields are derived from <c>UserInfo</c> and profile fields
/// from <c>UserProfile</c> (Library/Components/Users/**). Profile fields are kept
/// flat on this write DTO (rather than nesting a profile DTO) so that request
/// contracts remain independent of read/projection shapes.
/// <para>
/// SECURITY: this DTO intentionally contains NO credential fields of any kind.
/// Authentication credentials are changed exclusively through the dedicated
/// change-credential flow and its own request DTO, so an update request can
/// never be used to alter a user's login credentials.
/// </para>
/// </remarks>
public record UpdateUserDto
{
    // ---- Identity (MIGRATION: from UserInfo.vb) ----

    /// <summary>Gets the user's first (given) name. Required identity field.</summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Gets the user's last (family) name. Required identity field.</summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>Gets the optional display name shown in the UI; falls back to the full name when omitted.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets the user's e-mail address. Required identity field.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Gets the optional affiliate identifier associated with the user (admin toggle).</summary>
    public int? AffiliateID { get; init; }

    /// <summary>Gets the optional approval flag for the user account (admin toggle).</summary>
    public bool? Approved { get; init; }

    // ---- Profile (MIGRATION: flat, from UserProfile.vb) ----

    /// <summary>Gets the street portion of the user's postal address.</summary>
    public string? Street { get; init; }

    /// <summary>Gets the unit/apartment/suite portion of the user's postal address.</summary>
    public string? Unit { get; init; }

    /// <summary>Gets the city portion of the user's postal address.</summary>
    public string? City { get; init; }

    /// <summary>Gets the region/state/province portion of the user's postal address.</summary>
    public string? Region { get; init; }

    /// <summary>Gets the country portion of the user's postal address.</summary>
    public string? Country { get; init; }

    /// <summary>Gets the postal/ZIP code portion of the user's postal address.</summary>
    public string? PostalCode { get; init; }

    /// <summary>Gets the user's primary (landline) telephone number.</summary>
    public string? Telephone { get; init; }

    /// <summary>Gets the user's cell/mobile telephone number.</summary>
    public string? Cell { get; init; }

    /// <summary>Gets the user's fax number.</summary>
    public string? Fax { get; init; }

    /// <summary>Gets the user's website URL.</summary>
    public string? Website { get; init; }

    /// <summary>Gets the user's instant-messaging handle.</summary>
    public string? IM { get; init; }

    /// <summary>Gets the user's preferred locale (culture code, e.g. "en-US").</summary>
    public string? PreferredLocale { get; init; }

    /// <summary>Gets the user's time-zone offset. MIGRATION: mirrors UserProfile.TimeZone (Integer).</summary>
    public int TimeZone { get; init; }
}
