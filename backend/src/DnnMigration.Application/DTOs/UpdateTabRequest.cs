// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: This DTO is derived from TabController.vb UpdateTab method parameters
// and TabInfo.vb entity properties for partial update support.
// Source: Library/Components/Tabs/TabController.vb - UpdateTab method
// Source: Library/Components/Tabs/TabInfo.vb - Tab entity properties
// -----------------------------------------------------------------------------

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Data Transfer Object for tab/page update requests.
/// All properties are nullable to support partial updates via HTTP PUT requests.
/// Used as request body for PUT /api/tabs/{id} endpoint.
/// </summary>
/// <remarks>
/// MIGRATION: Fields derived from TabController.vb UpdateTab method which passes
/// the following parameters to DataProvider.Instance().UpdateTab:
/// TabID, TabName, IsVisible, DisableLink, ParentId, IconFile, Title, Description,
/// KeyWords, IsDeleted, Url, SkinSrc, ContainerSrc, TabPath, StartDate, EndDate,
/// RefreshInterval, PageHeadText, IsSecure
/// 
/// Note: TabID is provided via route parameter, not in request body.
/// Note: TabPath is computed server-side based on ParentId and TabName.
/// Note: IsDeleted is handled via DELETE endpoint, not through update.
/// </remarks>
public record UpdateTabRequest
{
    /// <summary>
    /// Gets or sets the tab display name shown in navigation.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.TabName property.
    /// When updated, triggers recalculation of TabPath for this tab and all children.
    /// </remarks>
    public string? TabName { get; init; }

    /// <summary>
    /// Gets or sets the parent tab identifier for hierarchical positioning.
    /// Use null or -1 for root-level tabs.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.ParentId property.
    /// When changed, the tab is moved in the hierarchy and child tab paths are updated.
    /// </remarks>
    public int? ParentId { get; init; }

    /// <summary>
    /// Gets or sets the display order of the tab within its parent level.
    /// Lower values appear first in navigation.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.TabOrder property.
    /// Used for reordering tabs within the same parent level.
    /// </remarks>
    public int? TabOrder { get; init; }

    /// <summary>
    /// Gets or sets the page title displayed in browser title bar and SEO.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.Title property.
    /// Used for HTML &lt;title&gt; element and search engine optimization.
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>
    /// Gets or sets the meta description for SEO purposes.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.Description property.
    /// Used for HTML meta description tag.
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the meta keywords for SEO purposes.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.KeyWords property.
    /// Used for HTML meta keywords tag (comma-separated values).
    /// </remarks>
    public string? KeyWords { get; init; }

    /// <summary>
    /// Gets or sets the icon file path for the tab in navigation.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.IconFile property.
    /// Path relative to portal home directory or absolute URL.
    /// </remarks>
    public string? IconFile { get; init; }

    /// <summary>
    /// Gets or sets whether the tab is visible in navigation menus.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.IsVisible property.
    /// Hidden tabs can still be accessed directly by URL if permissions allow.
    /// </remarks>
    public bool? IsVisible { get; init; }

    /// <summary>
    /// Gets or sets whether the tab link is disabled in navigation.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.DisableLink property.
    /// When true, the tab appears in navigation but is not clickable.
    /// Useful for parent containers that group child tabs.
    /// </remarks>
    public bool? DisableLink { get; init; }

    /// <summary>
    /// Gets or sets an external URL for redirection when the tab is accessed.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.Url property.
    /// When set, clicking the tab redirects to this URL instead of rendering content.
    /// Can be external URL or internal file/tab reference.
    /// </remarks>
    public string? Url { get; init; }

    /// <summary>
    /// Gets or sets the skin source file path for custom page appearance.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.SkinSrc property.
    /// Format: [S]PortalId/SkinFolder/SkinFile.ascx or [G]SkinFolder/SkinFile.ascx
    /// [S] = Site-specific skin, [G] = Global/Host skin
    /// </remarks>
    public string? SkinSrc { get; init; }

    /// <summary>
    /// Gets or sets the container source file path for module containers.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.ContainerSrc property.
    /// Format similar to SkinSrc. Defines default container for modules on this tab.
    /// </remarks>
    public string? ContainerSrc { get; init; }

    /// <summary>
    /// Gets or sets the date and time when the tab becomes visible.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.StartDate property.
    /// Used for scheduled publishing. Tab is hidden until this date.
    /// Null means no start date restriction.
    /// </remarks>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets or sets the date and time when the tab expires and becomes hidden.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.EndDate property.
    /// Used for scheduled content expiration. Tab is hidden after this date.
    /// Null means no end date restriction.
    /// </remarks>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets or sets the auto-refresh interval in seconds.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.RefreshInterval property.
    /// When greater than 0, the page automatically refreshes after this many seconds.
    /// Null or 0 disables auto-refresh.
    /// </remarks>
    public int? RefreshInterval { get; init; }

    /// <summary>
    /// Gets or sets custom HTML content to inject into the page head section.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.PageHeadText property.
    /// Allows injection of custom meta tags, scripts, or styles.
    /// Content is rendered within the &lt;head&gt; element.
    /// </remarks>
    public string? PageHeadText { get; init; }

    /// <summary>
    /// Gets or sets whether the tab requires HTTPS/SSL connection.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to TabInfo.IsSecure property.
    /// When true, accessing this tab over HTTP will redirect to HTTPS.
    /// </remarks>
    public bool? IsSecure { get; init; }
}
