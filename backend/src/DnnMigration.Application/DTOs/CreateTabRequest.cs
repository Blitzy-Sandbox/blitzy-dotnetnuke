// MIGRATION: Fields derived from TabController.vb AddTab method parameters.
// Original VB.NET method signature from TabController.vb line 334:
// DataProvider.Instance().AddTab(PortalID, TabName, IsVisible, DisableLink, ParentId, IconFile, 
//     Title, Description, KeyWords, Url, SkinSrc, ContainerSrc, TabPath, StartDate, EndDate,
//     RefreshInterval, PageHeadText, IsSecure)

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Data Transfer Object for tab/page creation requests.
/// Contains all required fields for creating a new tab in the navigation hierarchy.
/// Used as request body for POST /api/tabs endpoint.
/// </summary>
/// <remarks>
/// MIGRATION: This DTO maps to the TabInfo entity and corresponds to the AddTab method
/// in the legacy TabController.vb. Properties are derived from the VB.NET TabInfo class
/// and the AddTab stored procedure parameters.
/// </remarks>
public record CreateTabRequest
{
    /// <summary>
    /// Gets or sets the portal identifier that this tab belongs to.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.PortalID property.
    /// Required field for multi-tenant portal isolation.
    /// </remarks>
    [Required(ErrorMessage = "Portal ID is required.")]
    public int PortalId { get; init; }

    /// <summary>
    /// Gets or sets the display name of the tab/page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.TabName property.
    /// This is the visible name shown in navigation menus.
    /// </remarks>
    [Required(ErrorMessage = "Tab name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Tab name must be between 1 and 200 characters.")]
    public string TabName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent tab identifier for hierarchical navigation.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.ParentId property.
    /// Null value indicates a root-level tab. VB.NET used Null.NullInteger for unset parent.
    /// </remarks>
    public int? ParentId { get; init; }

    /// <summary>
    /// Gets or sets the page title for browser title bar and SEO.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.Title property.
    /// Used in HTML &lt;title&gt; element and SEO metadata.
    /// </remarks>
    [StringLength(500, ErrorMessage = "Title cannot exceed 500 characters.")]
    public string? Title { get; init; }

    /// <summary>
    /// Gets or sets the page description for SEO meta tags.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.Description property.
    /// Used in HTML meta description tag for search engine optimization.
    /// </remarks>
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the SEO keywords for the page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.KeyWords property.
    /// Comma-separated list of keywords for search engine optimization.
    /// </remarks>
    [StringLength(2000, ErrorMessage = "Keywords cannot exceed 2000 characters.")]
    public string? KeyWords { get; init; }

    /// <summary>
    /// Gets or sets the icon file path for navigation display.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.IconFile property.
    /// Relative path to the icon image file for menu/navigation display.
    /// </remarks>
    [StringLength(500, ErrorMessage = "Icon file path cannot exceed 500 characters.")]
    public string? IconFile { get; init; }

    /// <summary>
    /// Gets or sets whether the tab is visible in navigation menus.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.IsVisible property.
    /// Default is true to match legacy DNN behavior.
    /// </remarks>
    public bool IsVisible { get; init; } = true;

    /// <summary>
    /// Gets or sets whether the tab link is disabled (non-clickable).
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.DisableLink property.
    /// When true, the tab appears in navigation but cannot be clicked.
    /// Useful for parent tabs that only serve as containers for child tabs.
    /// </remarks>
    public bool DisableLink { get; init; } = false;

    /// <summary>
    /// Gets or sets an external URL for the tab (for redirect pages).
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.Url property.
    /// When set, clicking the tab navigates to this external URL instead of the page content.
    /// </remarks>
    [StringLength(500, ErrorMessage = "URL cannot exceed 500 characters.")]
    public string? Url { get; init; }

    /// <summary>
    /// Gets or sets the skin source file path for the tab's layout.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.SkinSrc property.
    /// Relative path to the skin/layout template file.
    /// </remarks>
    [StringLength(500, ErrorMessage = "Skin source path cannot exceed 500 characters.")]
    public string? SkinSrc { get; init; }

    /// <summary>
    /// Gets or sets the container source file path for module containers.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.ContainerSrc property.
    /// Relative path to the default container template for modules on this tab.
    /// </remarks>
    [StringLength(500, ErrorMessage = "Container source path cannot exceed 500 characters.")]
    public string? ContainerSrc { get; init; }

    /// <summary>
    /// Gets or sets the start date when the tab becomes visible.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.StartDate property.
    /// Used for scheduling tab visibility. Null means immediately visible.
    /// </remarks>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets or sets the end date when the tab becomes hidden.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.EndDate property.
    /// Used for scheduling tab visibility expiration. Null means no expiration.
    /// </remarks>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets or sets the auto-refresh interval in seconds for the page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.RefreshInterval property.
    /// When set, causes the page to automatically refresh at the specified interval.
    /// Null or 0 means no auto-refresh.
    /// </remarks>
    [Range(0, 86400, ErrorMessage = "Refresh interval must be between 0 and 86400 seconds (24 hours).")]
    public int? RefreshInterval { get; init; }

    /// <summary>
    /// Gets or sets custom HTML content to include in the page head section.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.PageHeadText property.
    /// Allows injection of custom scripts, styles, or meta tags into the HTML head.
    /// </remarks>
    [StringLength(5000, ErrorMessage = "Page head text cannot exceed 5000 characters.")]
    public string? PageHeadText { get; init; }

    /// <summary>
    /// Gets or sets whether the tab requires HTTPS/SSL.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo.IsSecure property.
    /// When true, accessing this tab redirects to HTTPS if not already secure.
    /// </remarks>
    public bool IsSecure { get; init; } = false;
}
