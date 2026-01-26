// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Portals.PortalInfo → C# 12 Portal entity
// Source: Library/Components/Portal/PortalInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted 30+ Private fields to C# auto-properties
// - Removed XmlRoot and XmlElement attributes (EF Core uses Fluent API)
// - Converted VB Single type to C# decimal for HostFee (better currency precision)
// - Converted VB Date type to C# DateTime? for ExpiryDate
// - Applied nullable reference types throughout (string? for optional strings)
// - Removed computed properties (Users, Pages, HomeDirectoryMapPath) - handled by services
// - Added navigation collections for Users, Modules, Tabs, Roles
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a portal (multi-tenant site container) in the DNN system.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the Portals table and is the core entity for DNN's multi-tenancy model.
/// Each portal represents a separate website with its own users, modules, tabs, and configuration.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Portals.PortalInfo.
/// HostFee converted from Single to decimal for better currency precision.
/// </para>
/// </remarks>
public class Portal
{
    /// <summary>
    /// Gets or sets the unique identifier for the portal.
    /// </summary>
    /// <value>The primary key of the portal record.</value>
    public int PortalId { get; set; }

    /// <summary>
    /// Gets or sets the name of the portal.
    /// </summary>
    /// <value>The display name for the portal.</value>
    public string PortalName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logo file path.
    /// </summary>
    /// <value>The relative path to the portal's logo file. May be null.</value>
    public string? LogoFile { get; set; }

    /// <summary>
    /// Gets or sets the footer text displayed on the portal.
    /// </summary>
    /// <value>The footer text content. May be null.</value>
    public string? FooterText { get; set; }

    /// <summary>
    /// Gets or sets the expiry date for the portal.
    /// </summary>
    /// <value>The date when the portal subscription expires. May be null for unlimited.</value>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets the user registration mode.
    /// </summary>
    /// <value>An integer representing the registration mode (see UserRegistrationType enum).</value>
    public int UserRegistration { get; set; }

    /// <summary>
    /// Gets or sets the banner advertising mode.
    /// </summary>
    /// <value>An integer representing the banner advertising setting (see BannerType enum).</value>
    public int BannerAdvertising { get; set; }

    /// <summary>
    /// Gets or sets the administrator user identifier.
    /// </summary>
    /// <value>The user ID of the portal administrator.</value>
    public int AdministratorId { get; set; }

    /// <summary>
    /// Gets or sets the currency code for the portal.
    /// </summary>
    /// <value>The currency code (e.g., "USD", "EUR"). May be null.</value>
    public string? Currency { get; set; }

    /// <summary>
    /// Gets or sets the host fee for the portal.
    /// </summary>
    /// <value>The hosting fee amount.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB Single to C# decimal for better currency precision.
    /// </remarks>
    public decimal HostFee { get; set; }

    /// <summary>
    /// Gets or sets the host space allocation in MB.
    /// </summary>
    /// <value>The allocated disk space in megabytes.</value>
    public int HostSpace { get; set; }

    /// <summary>
    /// Gets or sets the page quota for the portal.
    /// </summary>
    /// <value>The maximum number of pages allowed, or 0 for unlimited.</value>
    public int PageQuota { get; set; }

    /// <summary>
    /// Gets or sets the user quota for the portal.
    /// </summary>
    /// <value>The maximum number of users allowed, or 0 for unlimited.</value>
    public int UserQuota { get; set; }

    /// <summary>
    /// Gets or sets the administrator role identifier.
    /// </summary>
    /// <value>The role ID of the administrator role for this portal.</value>
    public int AdministratorRoleId { get; set; }

    /// <summary>
    /// Gets or sets the administrator role name.
    /// </summary>
    /// <value>The name of the administrator role. May be null.</value>
    public string? AdministratorRoleName { get; set; }

    /// <summary>
    /// Gets or sets the registered users role identifier.
    /// </summary>
    /// <value>The role ID of the registered users role for this portal.</value>
    public int RegisteredRoleId { get; set; }

    /// <summary>
    /// Gets or sets the registered users role name.
    /// </summary>
    /// <value>The name of the registered users role. May be null.</value>
    public string? RegisteredRoleName { get; set; }

    /// <summary>
    /// Gets or sets the description of the portal.
    /// </summary>
    /// <value>A description of the portal for SEO purposes. May be null.</value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the keywords for the portal.
    /// </summary>
    /// <value>SEO keywords for the portal. May be null.</value>
    public string? KeyWords { get; set; }

    /// <summary>
    /// Gets or sets the background file path.
    /// </summary>
    /// <value>The relative path to the background image. May be null.</value>
    public string? BackgroundFile { get; set; }

    /// <summary>
    /// Gets or sets the globally unique identifier for the portal.
    /// </summary>
    /// <value>A GUID that uniquely identifies this portal.</value>
    public Guid GUID { get; set; }

    /// <summary>
    /// Gets or sets the payment processor name.
    /// </summary>
    /// <value>The name of the payment processor. May be null.</value>
    public string? PaymentProcessor { get; set; }

    /// <summary>
    /// Gets or sets the processor user ID.
    /// </summary>
    /// <value>The user ID for the payment processor account. May be null.</value>
    public string? ProcessorUserId { get; set; }

    /// <summary>
    /// Gets or sets the processor password.
    /// </summary>
    /// <value>The password for the payment processor account. May be null.</value>
    public string? ProcessorPassword { get; set; }

    /// <summary>
    /// Gets or sets the site log history in days.
    /// </summary>
    /// <value>The number of days to keep site log entries.</value>
    public int SiteLogHistory { get; set; }

    /// <summary>
    /// Gets or sets the portal administrator email.
    /// </summary>
    /// <value>The email address for portal administration. May be null.</value>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the admin tab identifier.
    /// </summary>
    /// <value>The tab ID of the admin page.</value>
    public int AdminTabId { get; set; }

    /// <summary>
    /// Gets or sets the super tab identifier.
    /// </summary>
    /// <value>The tab ID of the super/host page.</value>
    public int SuperTabId { get; set; }

    /// <summary>
    /// Gets or sets the splash tab identifier.
    /// </summary>
    /// <value>The tab ID of the splash page.</value>
    public int SplashTabId { get; set; }

    /// <summary>
    /// Gets or sets the home tab identifier.
    /// </summary>
    /// <value>The tab ID of the home page.</value>
    public int HomeTabId { get; set; }

    /// <summary>
    /// Gets or sets the login tab identifier.
    /// </summary>
    /// <value>The tab ID of the login page.</value>
    public int LoginTabId { get; set; }

    /// <summary>
    /// Gets or sets the user tab identifier.
    /// </summary>
    /// <value>The tab ID of the user profile page.</value>
    public int UserTabId { get; set; }

    /// <summary>
    /// Gets or sets the default language for the portal.
    /// </summary>
    /// <value>The default language code (e.g., "en-US"). May be null.</value>
    public string? DefaultLanguage { get; set; }

    /// <summary>
    /// Gets or sets the time zone offset in minutes.
    /// </summary>
    /// <value>The time zone offset from UTC.</value>
    public int TimeZoneOffset { get; set; }

    /// <summary>
    /// Gets or sets the home directory path.
    /// </summary>
    /// <value>The relative path to the portal's home directory. May be null.</value>
    public string? HomeDirectory { get; set; }

    /// <summary>
    /// Gets or sets the version string.
    /// </summary>
    /// <value>The DNN version for this portal. May be null.</value>
    public string? Version { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the collection of users belonging to this portal.
    /// </summary>
    /// <value>A collection of <see cref="User"/> entities.</value>
    public virtual ICollection<User> Users { get; set; } = new List<User>();

    /// <summary>
    /// Gets or sets the collection of modules in this portal.
    /// </summary>
    /// <value>A collection of <see cref="Module"/> entities.</value>
    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();

    /// <summary>
    /// Gets or sets the collection of tabs (pages) in this portal.
    /// </summary>
    /// <value>A collection of <see cref="Tab"/> entities.</value>
    public virtual ICollection<Tab> Tabs { get; set; } = new List<Tab>();

    /// <summary>
    /// Gets or sets the collection of roles in this portal.
    /// </summary>
    /// <value>A collection of <see cref="Role"/> entities.</value>
    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    /// <summary>
    /// Gets or sets the collection of portal aliases for this portal.
    /// </summary>
    /// <value>A collection of <see cref="PortalAlias"/> entities.</value>
    public virtual ICollection<PortalAlias> PortalAliases { get; set; } = new List<PortalAlias>();
}
