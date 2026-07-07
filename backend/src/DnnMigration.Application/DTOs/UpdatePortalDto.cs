using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload used to update an existing portal's core settings.
/// </summary>
/// <remarks>
/// Bound from the body of <c>PUT /api/portals/{id}</c>. Validation rules for this
/// payload are owned by <c>PortalValidator</c> (FluentValidation) and entity
/// projection is handled by <c>MappingProfile</c> (AutoMapper); this type therefore
/// intentionally carries no validation attributes, no business logic, and no
/// data-access concerns, and it is not wrapped in the <c>{ data, meta }</c> envelope.
///
/// MIGRATION: the field set mirrors the legacy
/// <c>PortalController.UpdatePortalInfo</c> overload
/// (<c>Library/Components/Portal/PortalController.vb</c> L1568), which is the exact
/// set of updatable portal attributes surfaced by the original Site Settings admin
/// screen. The portal identifier is taken from the route (<c>{id}</c>), so it is not
/// carried in the body and there is no <c>PortalID</c> property here.
///
/// MIGRATION (type-mirroring): property CLR types mirror the <c>Portal</c> domain
/// entity (derived from <c>PortalInfo.vb</c>) rather than the widened parameter types
/// of the legacy controller overload, so AutoMapper convention mapping requires no
/// custom value resolvers. In particular <see cref="HostFee"/> is <c>float</c>
/// (legacy <c>PortalInfo._HostFee As Single</c>) even though the controller overload
/// widened it to <c>Double</c>, and <see cref="HostSpace"/> is <c>int</c> (legacy
/// <c>PortalInfo._HostSpace As Integer</c>). Unlike the read-side <c>PortalDto</c>,
/// this write DTO deliberately includes the payment-processor fields
/// (<see cref="PaymentProcessor"/>, <see cref="ProcessorUserId"/>,
/// <see cref="ProcessorPassword"/>) because the update screen sets them.
/// </remarks>
public record UpdatePortalDto
{
    /// <summary>
    /// Display name of the portal. MIGRATION: legacy <c>PortalInfo.PortalName</c>
    /// (VB <c>String</c>); required and therefore defaulted to an empty string.
    /// </summary>
    public string PortalName { get; init; } = string.Empty;

    /// <summary>
    /// Optional relative path to the portal logo image.
    /// MIGRATION: legacy <c>PortalInfo.LogoFile</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? LogoFile { get; init; }

    /// <summary>
    /// Optional footer text rendered across the portal's pages.
    /// MIGRATION: legacy <c>PortalInfo.FooterText</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? FooterText { get; init; }

    /// <summary>
    /// Date on which the portal subscription expires.
    /// MIGRATION: legacy <c>PortalInfo.ExpiryDate</c> (VB <c>Date</c> =&gt; <see cref="System.DateTime"/>).
    /// </summary>
    public DateTime ExpiryDate { get; init; }

    /// <summary>
    /// User-registration mode governing how visitors may create accounts.
    /// MIGRATION: legacy <c>PortalInfo.UserRegistration</c> (VB <c>Integer</c>)
    /// modeled as the strongly-typed <see cref="UserRegistrationType"/> enum.
    /// </summary>
    public UserRegistrationType UserRegistration { get; init; }

    /// <summary>
    /// Banner advertising rendering format for the portal.
    /// MIGRATION: legacy <c>PortalInfo.BannerAdvertising</c> (VB <c>Integer</c>)
    /// modeled as the strongly-typed <see cref="BannerType"/> enum.
    /// </summary>
    public BannerType BannerAdvertising { get; init; }

    /// <summary>
    /// Optional ISO currency code used for portal billing/pricing.
    /// MIGRATION: legacy <c>PortalInfo.Currency</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>
    /// Identifier of the user designated as the portal administrator.
    /// MIGRATION: legacy <c>PortalInfo.AdministratorId</c> (VB <c>Integer</c>).
    /// </summary>
    public int AdministratorId { get; init; }

    /// <summary>
    /// Recurring hosting fee charged for the portal.
    /// MIGRATION: legacy <c>PortalInfo._HostFee As Single</c> =&gt; <c>float</c>
    /// (entity type mirrored, not the controller overload's <c>Double</c>).
    /// </summary>
    public float HostFee { get; init; }

    /// <summary>
    /// Disk-space allocation, in megabytes, granted to the portal (0 = unlimited).
    /// MIGRATION: legacy <c>PortalInfo._HostSpace As Integer</c> =&gt; <c>int</c>
    /// (entity type mirrored, not the controller overload's <c>Double</c>).
    /// </summary>
    public int HostSpace { get; init; }

    /// <summary>
    /// Maximum number of pages (tabs) permitted for the portal (0 = unlimited).
    /// MIGRATION: legacy <c>PortalInfo.PageQuota</c> (VB <c>Integer</c>).
    /// </summary>
    public int PageQuota { get; init; }

    /// <summary>
    /// Maximum number of users permitted for the portal (0 = unlimited).
    /// MIGRATION: legacy <c>PortalInfo.UserQuota</c> (VB <c>Integer</c>).
    /// </summary>
    public int UserQuota { get; init; }

    /// <summary>
    /// Optional name of the payment processor used for portal subscriptions.
    /// MIGRATION: legacy <c>PortalInfo.PaymentProcessor</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? PaymentProcessor { get; init; }

    /// <summary>
    /// Optional merchant/user identifier registered with the payment processor.
    /// MIGRATION: legacy <c>PortalInfo.ProcessorUserId</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? ProcessorUserId { get; init; }

    /// <summary>
    /// Optional credential/password registered with the payment processor.
    /// MIGRATION: legacy <c>PortalInfo.ProcessorPassword</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? ProcessorPassword { get; init; }

    /// <summary>
    /// Optional free-text description of the portal (used for SEO metadata).
    /// MIGRATION: legacy <c>PortalInfo.Description</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional comma/space separated keywords describing the portal (SEO metadata).
    /// MIGRATION: legacy <c>PortalInfo.KeyWords</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? KeyWords { get; init; }

    /// <summary>
    /// Optional relative path to the portal background image.
    /// MIGRATION: legacy <c>PortalInfo.BackgroundFile</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? BackgroundFile { get; init; }

    /// <summary>
    /// Number of days of site-log history retained for the portal.
    /// MIGRATION: legacy <c>PortalInfo.SiteLogHistory</c> (VB <c>Integer</c>).
    /// </summary>
    public int SiteLogHistory { get; init; }

    /// <summary>
    /// Tab (page) identifier displayed as the portal splash page.
    /// MIGRATION: legacy <c>PortalInfo.SplashTabId</c> (VB <c>Integer</c>).
    /// </summary>
    public int SplashTabId { get; init; }

    /// <summary>
    /// Tab (page) identifier used as the portal home page.
    /// MIGRATION: legacy <c>PortalInfo.HomeTabId</c> (VB <c>Integer</c>).
    /// </summary>
    public int HomeTabId { get; init; }

    /// <summary>
    /// Tab (page) identifier used as the portal login page.
    /// MIGRATION: legacy <c>PortalInfo.LoginTabId</c> (VB <c>Integer</c>).
    /// </summary>
    public int LoginTabId { get; init; }

    /// <summary>
    /// Tab (page) identifier used as the portal user-profile page.
    /// MIGRATION: legacy <c>PortalInfo.UserTabId</c> (VB <c>Integer</c>).
    /// </summary>
    public int UserTabId { get; init; }

    /// <summary>
    /// Optional default language/culture code for the portal (e.g. <c>en-US</c>).
    /// MIGRATION: legacy <c>PortalInfo.DefaultLanguage</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? DefaultLanguage { get; init; }

    /// <summary>
    /// Time-zone offset, in minutes, applied to portal date/time rendering.
    /// MIGRATION: legacy <c>PortalInfo.TimeZoneOffset</c> (VB <c>Integer</c>).
    /// </summary>
    public int TimeZoneOffset { get; init; }

    /// <summary>
    /// Optional home directory path for portal-scoped file storage.
    /// MIGRATION: legacy <c>PortalInfo.HomeDirectory</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? HomeDirectory { get; init; }
}
