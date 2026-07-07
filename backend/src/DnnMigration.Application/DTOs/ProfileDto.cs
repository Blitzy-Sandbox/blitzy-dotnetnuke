namespace DnnMigration.Application.DTOs;

/// <summary>
/// Read-only projection of a user's profile (address, contact, locale).
/// MIGRATION: mapped from the legacy DotNetNuke UserProfile entity
/// (Library/Components/Users/Profile/UserProfile.vb). Only the strongly-typed
/// address/contact/locale fields are surfaced; the legacy dynamic profile
/// property store (ProfilePropertyDefinitionCollection) is intentionally not
/// ported. This DTO carries no behaviour, mapping, or validation - those
/// concerns live in the sibling Mapping/ and Validators/ folders and in the
/// API layer respectively.
/// </summary>
public record ProfileDto
{
    public string Street { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Telephone { get; init; } = string.Empty;
    public string Cell { get; init; } = string.Empty;
    public string Fax { get; init; } = string.Empty;
    public string Website { get; init; } = string.Empty;
    public string IM { get; init; } = string.Empty;
    public int TimeZone { get; init; }
    public string PreferredLocale { get; init; } = string.Empty;
}
