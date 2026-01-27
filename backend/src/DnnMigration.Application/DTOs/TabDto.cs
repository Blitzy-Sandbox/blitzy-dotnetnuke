// -----------------------------------------------------------------------------
// DnnMigration - Tab Data Transfer Object
// MIGRATION: Properties mapped from Library/Components/Tabs/TabInfo.vb entity
// Converted from VB.NET to C# 12 with nullable reference types
// -----------------------------------------------------------------------------

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Data Transfer Object representing tab/page response data for API outputs.
/// Contains all tab properties for navigation hierarchy and page configuration.
/// </summary>
/// <remarks>
/// MIGRATION: This DTO is converted from DotNetNuke.Entities.Tabs.TabInfo (TabInfo.vb).
/// Properties are mapped from the legacy entity with VB.NET Null handling converted
/// to C# nullable reference types.
/// </remarks>
public record TabDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the tab.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._TabID (Integer)</remarks>
    public int TabId { get; init; }

    /// <summary>
    /// Gets or sets the display order of the tab within its parent container.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._TabOrder (Integer)</remarks>
    public int TabOrder { get; init; }

    /// <summary>
    /// Gets or sets the portal identifier that owns this tab.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._PortalID (Integer)</remarks>
    public int PortalId { get; init; }

    /// <summary>
    /// Gets or sets the display name of the tab shown in navigation.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._TabName (String)</remarks>
    public string TabName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent tab identifier for hierarchical navigation.
    /// Null indicates a root-level tab.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._ParentId (Integer).
    /// VB.NET Null.NullInteger converted to nullable int.
    /// </remarks>
    public int? ParentId { get; init; }

    /// <summary>
    /// Gets or sets the depth level of the tab in the navigation hierarchy.
    /// Root tabs have Level 0.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._Level (Integer)</remarks>
    public int Level { get; init; }

    /// <summary>
    /// Gets or sets whether the tab is visible in navigation menus.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._IsVisible (Boolean)</remarks>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Gets or sets the page title displayed in browser title bar and SEO.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._Title (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>
    /// Gets or sets the page description for SEO purposes.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._Description (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the SEO keywords for the page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._KeyWords (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? KeyWords { get; init; }

    /// <summary>
    /// Gets or sets the path to the icon file for the tab.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._IconFile (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? IconFile { get; init; }

    /// <summary>
    /// Gets or sets whether the tab link is disabled in navigation.
    /// When true, the tab displays but cannot be clicked.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._DisableLink (Boolean)</remarks>
    public bool DisableLink { get; init; }

    /// <summary>
    /// Gets or sets the external URL for the tab.
    /// When set, clicking the tab navigates to this URL instead of rendering content.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._Url (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? Url { get; init; }

    /// <summary>
    /// Gets or sets the skin source path for the tab.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._SkinSrc (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? SkinSrc { get; init; }

    /// <summary>
    /// Gets or sets the container source path for the tab.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._ContainerSrc (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? ContainerSrc { get; init; }

    /// <summary>
    /// Gets or sets the hierarchical path representation of the tab.
    /// Example: "//Home//About//Team"
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._TabPath (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? TabPath { get; init; }

    /// <summary>
    /// Gets or sets the date when the tab becomes visible.
    /// Null indicates no start date restriction.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._StartDate (Date).
    /// VB.NET Null.NullDate converted to nullable DateTime.
    /// </remarks>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets or sets the date when the tab is hidden.
    /// Null indicates no end date restriction.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._EndDate (Date).
    /// VB.NET Null.NullDate converted to nullable DateTime.
    /// </remarks>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets or sets the auto-refresh interval in seconds.
    /// Null indicates no auto-refresh.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._RefreshInterval (Integer).
    /// VB.NET Null.NullInteger converted to nullable int.
    /// </remarks>
    public int? RefreshInterval { get; init; }

    /// <summary>
    /// Gets or sets custom HTML content to be rendered in the page head section.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Maps to TabInfo._PageHeadText (String).
    /// VB.NET Null.NullString converted to nullable string.
    /// </remarks>
    public string? PageHeadText { get; init; }

    /// <summary>
    /// Gets or sets whether the tab requires HTTPS.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._IsSecure (Boolean)</remarks>
    public bool IsSecure { get; init; }

    /// <summary>
    /// Gets or sets whether the tab has been soft deleted.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._IsDeleted (Boolean)</remarks>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Gets or sets whether the tab has child tabs in the hierarchy.
    /// Used for navigation tree rendering optimization.
    /// </summary>
    /// <remarks>MIGRATION: Maps to TabInfo._HasChildren (Boolean)</remarks>
    public bool HasChildren { get; init; }
}
