namespace DnnMigration.Application.DTOs.User;

// MIGRATION: Flat profile DTO promoted from the legacy DotNetNuke.Entities.Users.UserProfile
// (Library/Components/Users/Profile/UserProfile.vb). The legacy profile stored values in a dynamic
// ProfilePropertyDefinitionCollection ("ProfileProperties"); its strongly-typed accessors are flattened here
// into plain properties. Runtime-only members IsDirty, ObjectHydrated, and ProfileProperties are intentionally
// dropped (persistence/hydration bookkeeping, not profile data).
public record UserProfileDto
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    // MIGRATION: Legacy UserProfile.FullName was a read-only computed property (FirstName & " " & LastName).
    // Exposed here as a plain field; the Application/mapping layer composes the value (no computation in the DTO).
    public string? FullName { get; init; }

    public string? Cell { get; init; }

    public string? Telephone { get; init; }

    public string? Fax { get; init; }

    // MIGRATION: Instant-messenger handle. Legacy property name "IM" preserved verbatim (do not rename to "Im").
    public string? IM { get; init; }

    public string? Street { get; init; }

    public string? Unit { get; init; }

    public string? City { get; init; }

    public string? Region { get; init; }

    public string? Country { get; init; }

    public string? PostalCode { get; init; }

    public string? PreferredLocale { get; init; }

    // MIGRATION: Legacy UserProfile.TimeZone was an Integer (getter parsed the stored string; Null.NullInteger
    // when unset). Kept as a plain int per the DTO contract (the service/mapping layer supplies the default).
    public int TimeZone { get; init; }

    public string? Website { get; init; }
}
