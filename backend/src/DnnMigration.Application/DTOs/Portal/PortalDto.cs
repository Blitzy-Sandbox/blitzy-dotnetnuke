// -----------------------------------------------------------------------
// <copyright file="PortalDto.cs" company="DNN Migration Project">
// MIT License - DotNetNuke Migration to .NET 8
// </copyright>
// <summary>
// Portal Data Transfer Object for API responses.
// MIGRATION: VB.NET PortalInfo (Library/Components/Portal/PortalInfo.vb) → C# 12 PortalDto record.
// Contains all portal properties for read operations.
// </summary>
// -----------------------------------------------------------------------

namespace DnnMigration.Application.DTOs.Portal;

/// <summary>
/// Data Transfer Object representing a Portal for API responses.
/// Contains identity, branding, administration, quotas/billing, registration settings,
/// navigation tabs, and localization properties.
/// </summary>
/// <remarks>
/// MIGRATION: Converted from VB.NET PortalInfo class to C# 12 record type for immutability and value semantics.
/// ProcessorPassword is intentionally excluded from this DTO for security reasons.
/// </remarks>
public record PortalDto
{
    /// <summary>
    /// Gets the unique identifier for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.PortalID property.</remarks>
    public int PortalId { get; init; }

    /// <summary>
    /// Gets the name of the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.PortalName property.</remarks>
    public required string PortalName { get; init; }

    /// <summary>
    /// Gets the relative path to the portal logo file.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.LogoFile property.</remarks>
    public string? LogoFile { get; init; }

    /// <summary>
    /// Gets the footer text displayed on the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.FooterText property.</remarks>
    public string? FooterText { get; init; }

    /// <summary>
    /// Gets the expiry date of the portal subscription.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.ExpiryDate property. Nullable to indicate no expiration.</remarks>
    public DateTime? ExpiryDate { get; init; }

    /// <summary>
    /// Gets the user registration mode for the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to PortalInfo.UserRegistration property.
    /// Values: 0 = None, 1 = Private, 2 = Public, 3 = Verified.
    /// </remarks>
    public int UserRegistration { get; init; }

    /// <summary>
    /// Gets the banner advertising mode for the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to PortalInfo.BannerAdvertising property.
    /// Values: 0 = None, 1 = Site, 2 = Vendor.
    /// </remarks>
    public int BannerAdvertising { get; init; }

    /// <summary>
    /// Gets the user ID of the portal administrator.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.AdministratorId property.</remarks>
    public int AdministratorId { get; init; }

    /// <summary>
    /// Gets the currency code for the portal (e.g., "USD", "EUR").
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.Currency property.</remarks>
    public string? Currency { get; init; }

    /// <summary>
    /// Gets the monthly hosting fee for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.HostFee property. Converted from Single to decimal for precision.</remarks>
    public decimal HostFee { get; init; }

    /// <summary>
    /// Gets the allocated disk space for the portal in megabytes.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.HostSpace property.</remarks>
    public int HostSpace { get; init; }

    /// <summary>
    /// Gets the maximum number of pages allowed for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.PageQuota property. 0 = unlimited.</remarks>
    public int PageQuota { get; init; }

    /// <summary>
    /// Gets the maximum number of users allowed for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.UserQuota property. 0 = unlimited.</remarks>
    public int UserQuota { get; init; }

    /// <summary>
    /// Gets the role ID of the administrator role for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.AdministratorRoleId property.</remarks>
    public int AdministratorRoleId { get; init; }

    /// <summary>
    /// Gets the name of the administrator role for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.AdministratorRoleName property.</remarks>
    public string? AdministratorRoleName { get; init; }

    /// <summary>
    /// Gets the role ID of the registered users role for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.RegisteredRoleId property.</remarks>
    public int RegisteredRoleId { get; init; }

    /// <summary>
    /// Gets the name of the registered users role for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.RegisteredRoleName property.</remarks>
    public string? RegisteredRoleName { get; init; }

    /// <summary>
    /// Gets the description of the portal for SEO and display purposes.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.Description property.</remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the keywords associated with the portal for SEO purposes.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.KeyWords property.</remarks>
    public string? KeyWords { get; init; }

    /// <summary>
    /// Gets the relative path to the background image file.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.BackgroundFile property.</remarks>
    public string? BackgroundFile { get; init; }

    /// <summary>
    /// Gets the globally unique identifier for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.GUID property.</remarks>
    public Guid GUID { get; init; }

    /// <summary>
    /// Gets the payment processor identifier (e.g., "PayPal", "Stripe").
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.PaymentProcessor property.</remarks>
    public string? PaymentProcessor { get; init; }

    /// <summary>
    /// Gets the user ID for the payment processor account.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to PortalInfo.ProcessorUserId property.
    /// Note: ProcessorPassword is intentionally excluded from this DTO for security.
    /// </remarks>
    public string? ProcessorUserId { get; init; }

    /// <summary>
    /// Gets the number of days to retain site log history.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.SiteLogHistory property.</remarks>
    public int SiteLogHistory { get; init; }

    /// <summary>
    /// Gets the primary email address for the portal.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.Email property.</remarks>
    public string? Email { get; init; }

    /// <summary>
    /// Gets the tab ID for the administration page.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.AdminTabId property.</remarks>
    public int AdminTabId { get; init; }

    /// <summary>
    /// Gets the tab ID for the super user (host) page.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.SuperTabId property.</remarks>
    public int SuperTabId { get; init; }

    /// <summary>
    /// Gets the tab ID for the splash/landing page.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.SplashTabId property.</remarks>
    public int SplashTabId { get; init; }

    /// <summary>
    /// Gets the tab ID for the home page.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.HomeTabId property.</remarks>
    public int HomeTabId { get; init; }

    /// <summary>
    /// Gets the tab ID for the login page.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.LoginTabId property.</remarks>
    public int LoginTabId { get; init; }

    /// <summary>
    /// Gets the tab ID for the user profile/registration page.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.UserTabId property.</remarks>
    public int UserTabId { get; init; }

    /// <summary>
    /// Gets the default language code for the portal (e.g., "en-US").
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.DefaultLanguage property.</remarks>
    public string? DefaultLanguage { get; init; }

    /// <summary>
    /// Gets the time zone offset in minutes from UTC.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.TimeZoneOffset property.</remarks>
    public int TimeZoneOffset { get; init; }

    /// <summary>
    /// Gets the home directory path relative to the application root.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.HomeDirectory property.</remarks>
    public string? HomeDirectory { get; init; }

    /// <summary>
    /// Gets the portal version string.
    /// </summary>
    /// <remarks>MIGRATION: Maps to PortalInfo.Version property.</remarks>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the total number of users registered in the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to PortalInfo.Users property.
    /// This is a computed value calculated at the service layer.
    /// </remarks>
    public int Users { get; init; }

    /// <summary>
    /// Gets the total number of pages (tabs) in the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to PortalInfo.Pages property.
    /// This is a computed value calculated at the service layer.
    /// </remarks>
    public int Pages { get; init; }
}
