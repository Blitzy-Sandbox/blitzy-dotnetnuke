using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Read-only projection of a portal, returned by <c>GET /api/portals</c> and
/// <c>GET /api/portals/{id}</c>.
/// </summary>
/// <remarks>
/// Projected from the Domain entity <c>DnnMigration.Domain.Entities.Portal</c> by
/// <c>MappingProfile</c> (AutoMapper). The property names and CLR types mirror the entity
/// one-to-one so the projection resolves by AutoMapper convention with no custom value
/// converters. This type is a pure data carrier: it intentionally contains no logic, no
/// data-access concerns, and no validation/AutoMapper attributes, and it is never wrapped in
/// the <c>{ data, meta }</c> response envelope here (the API layer applies that envelope).
///
/// MIGRATION: mapped from the legacy DotNetNuke entity <c>PortalInfo</c>
/// (Library/Components/Portal/PortalInfo.vb). The enum-typed members
/// (<see cref="UserRegistration"/>, <see cref="BannerAdvertising"/>) replace the legacy
/// integer-backed properties, and the payment-processor secrets
/// (<c>PaymentProcessor</c>, <c>ProcessorUserId</c>, <c>ProcessorPassword</c>) are
/// intentionally excluded from this read boundary and never serialized to clients.
/// </remarks>
public record PortalDto
{
    /// <summary>Primary key identifier of the portal.</summary>
    public int PortalID { get; init; }

    /// <summary>Display name of the portal.</summary>
    public string PortalName { get; init; } = string.Empty;

    /// <summary>Relative path to the portal logo image.</summary>
    public string LogoFile { get; init; } = string.Empty;

    /// <summary>HTML/text rendered in the portal footer.</summary>
    public string FooterText { get; init; } = string.Empty;

    /// <summary>Date on which the portal subscription expires.</summary>
    public DateTime ExpiryDate { get; init; }

    /// <summary>User-registration mode configured for the portal.</summary>
    public UserRegistrationType UserRegistration { get; init; }

    /// <summary>Banner advertising rendering format configured for the portal.</summary>
    public BannerType BannerAdvertising { get; init; }

    /// <summary>ISO currency code used for portal billing/host-fee amounts.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>User identifier of the portal administrator.</summary>
    public int AdministratorId { get; init; }

    /// <summary>Recurring hosting fee charged for the portal. MIGRATION: legacy VB <c>Single</c> maps to <see cref="float"/>.</summary>
    public float HostFee { get; init; }

    /// <summary>Disk space quota (in megabytes) allotted to the portal.</summary>
    public int HostSpace { get; init; }

    /// <summary>Maximum number of pages permitted for the portal.</summary>
    public int PageQuota { get; init; }

    /// <summary>Maximum number of users permitted for the portal.</summary>
    public int UserQuota { get; init; }

    /// <summary>Free-text description of the portal.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Search keywords associated with the portal.</summary>
    public string KeyWords { get; init; } = string.Empty;

    /// <summary>Relative path to the portal background image.</summary>
    public string BackgroundFile { get; init; } = string.Empty;

    /// <summary>Number of days of site-log history retained for the portal.</summary>
    public int SiteLogHistory { get; init; }

    /// <summary>Tab (page) identifier of the portal splash page.</summary>
    public int SplashTabId { get; init; }

    /// <summary>Tab (page) identifier of the portal home page.</summary>
    public int HomeTabId { get; init; }

    /// <summary>Tab (page) identifier of the portal login page.</summary>
    public int LoginTabId { get; init; }

    /// <summary>Tab (page) identifier of the portal user-registration page.</summary>
    public int UserTabId { get; init; }

    /// <summary>Default culture/language code for the portal.</summary>
    public string DefaultLanguage { get; init; } = string.Empty;

    /// <summary>Time-zone offset (in minutes) applied to portal times.</summary>
    public int TimeZoneOffset { get; init; }

    /// <summary>Root storage directory for portal-scoped files.</summary>
    public string HomeDirectory { get; init; } = string.Empty;

    /// <summary>Role identifier granting portal administrator privileges.</summary>
    public int AdministratorRoleId { get; init; }

    /// <summary>Role identifier assigned to registered users.</summary>
    public int RegisteredRoleId { get; init; }

    /// <summary>Contact email address for the portal.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Tab (page) identifier of the portal admin page.</summary>
    public int AdminTabId { get; init; }

    /// <summary>Current number of users belonging to the portal.</summary>
    public int Users { get; init; }

    /// <summary>Current number of pages belonging to the portal.</summary>
    public int Pages { get; init; }

    /// <summary>Globally unique identifier of the portal.</summary>
    public Guid GUID { get; init; }

    /// <summary>Portal schema/version stamp.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// The portal's configured HTTP aliases (host names). Empty when the portal has none.
    /// </summary>
    /// <remarks>
    /// MIGRATION: reinstates the legacy Portals.ascx.vb grid "Portal Aliases" column
    /// (FormatPortalAliases(PortalID), which listed PortalAlias.HTTPAlias values per portal). The
    /// underlying <c>Portal</c> entity intentionally carries no PortalAlias navigation collection (to keep
    /// the EF model/snapshot unchanged), so this member is NOT populated by AutoMapper convention — it is
    /// filled by <c>PortalService</c> from a dedicated alias lookup and is explicitly ignored in the
    /// mapping profile. Consumed by the Angular portal-list "Portal Aliases" column.
    /// </remarks>
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
}
