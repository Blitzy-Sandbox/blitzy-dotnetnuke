// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Tabs.TabInfo → C# 12 Tab entity
// Source: Library/Components/Tabs/TabInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted Private fields (_TabID, _TabName, etc.) to C# auto-properties
// - Removed IPropertyAccess implementation (token replacement)
// - Removed XmlElement, XmlArray, XmlIgnore attributes (EF Core uses Fluent API)
// - Removed runtime-only properties (BreadCrumbs, Panes, Modules, SkinPath, ContainerPath)
// - Converted VB Date to C# DateTime?
// - Applied nullable reference types
// - Added navigation properties for Portal, ParentTab, ChildTabs, and Modules
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a tab (page) within a portal's navigation hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the Tabs table and represents a page in the portal navigation.
/// Tabs form a hierarchical structure through the ParentId relationship and contain
/// multiple <see cref="Module"/> instances.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Tabs.TabInfo.
/// Runtime-only properties (BreadCrumbs, Panes, etc.) removed - handled by services.
/// </para>
/// </remarks>
public class Tab
{
    /// <summary>
    /// Gets or sets the unique identifier for the tab.
    /// </summary>
    /// <value>The primary key of the tab record.</value>
    public int TabId { get; set; }

    /// <summary>
    /// Gets or sets the portal identifier that this tab belongs to.
    /// </summary>
    /// <value>The foreign key to the Portal entity.</value>
    public int PortalId { get; set; }

    /// <summary>
    /// Gets or sets the order of the tab within its level.
    /// </summary>
    /// <value>The display order among sibling tabs.</value>
    public int TabOrder { get; set; }

    /// <summary>
    /// Gets or sets the name of the tab.
    /// </summary>
    /// <value>The display name for the tab in navigation.</value>
    public string TabName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the tab is visible in navigation.
    /// </summary>
    /// <value><c>true</c> if the tab is visible; otherwise, <c>false</c>.</value>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Gets or sets the parent tab identifier.
    /// </summary>
    /// <value>The foreign key to the parent Tab, or null for root tabs.</value>
    public int? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the nesting level of the tab in the hierarchy.
    /// </summary>
    /// <value>0 for root tabs, incrementing for nested tabs.</value>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the icon file path.
    /// </summary>
    /// <value>The relative path to the tab's icon file. May be null.</value>
    public string? IconFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab link is disabled.
    /// </summary>
    /// <value><c>true</c> if the link is disabled; otherwise, <c>false</c>.</value>
    public bool DisableLink { get; set; }

    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    /// <value>The HTML title for the page. May be null.</value>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the page description.
    /// </summary>
    /// <value>The meta description for SEO. May be null.</value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the page keywords.
    /// </summary>
    /// <value>The meta keywords for SEO. May be null.</value>
    public string? KeyWords { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab is deleted (soft delete).
    /// </summary>
    /// <value><c>true</c> if the tab is deleted; otherwise, <c>false</c>.</value>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the URL for external links or redirects.
    /// </summary>
    /// <value>The URL to redirect to. May be null for normal tabs.</value>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the skin source path.
    /// </summary>
    /// <value>The path to the skin template. May be null for default skin.</value>
    public string? SkinSrc { get; set; }

    /// <summary>
    /// Gets or sets the container source path.
    /// </summary>
    /// <value>The path to the container template. May be null for default container.</value>
    public string? ContainerSrc { get; set; }

    /// <summary>
    /// Gets or sets the tab path in the hierarchy.
    /// </summary>
    /// <value>The hierarchical path of the tab (e.g., "//Home//About"). May be null.</value>
    public string? TabPath { get; set; }

    /// <summary>
    /// Gets or sets the start date for tab visibility.
    /// </summary>
    /// <value>The date when the tab becomes visible. May be null for always visible.</value>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for tab visibility.
    /// </summary>
    /// <value>The date when the tab becomes hidden. May be null for never ending.</value>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab has child tabs.
    /// </summary>
    /// <value><c>true</c> if the tab has children; otherwise, <c>false</c>.</value>
    public bool HasChildren { get; set; }

    /// <summary>
    /// Gets or sets the refresh interval for the page in seconds.
    /// </summary>
    /// <value>The meta refresh interval, or null/0 for no auto-refresh.</value>
    public int? RefreshInterval { get; set; }

    /// <summary>
    /// Gets or sets custom HTML to include in the page head.
    /// </summary>
    /// <value>Custom HTML for the head section. May be null.</value>
    public string? PageHeadText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab requires secure (HTTPS) access.
    /// </summary>
    /// <value><c>true</c> if HTTPS is required; otherwise, <c>false</c>.</value>
    public bool IsSecure { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a super tab (host-level).
    /// </summary>
    /// <value><c>true</c> if this is a host-level tab; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Originally computed based on PortalID == NullInteger.
    /// </remarks>
    public bool IsSuperTab { get; set; }

    /// <summary>
    /// Gets or sets the authorized roles for viewing this tab.
    /// </summary>
    /// <value>A semicolon-separated list of role IDs. May be null.</value>
    /// <remarks>
    /// MIGRATION: Legacy property for basic authorization. Consider using TabPermissions instead.
    /// </remarks>
    public string? AuthorizedRoles { get; set; }

    /// <summary>
    /// Gets or sets the administrator roles for managing this tab.
    /// </summary>
    /// <value>A semicolon-separated list of role IDs. May be null.</value>
    /// <remarks>
    /// MIGRATION: Legacy property for basic authorization. Consider using TabPermissions instead.
    /// </remarks>
    public string? AdministratorRoles { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the portal that this tab belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Portal"/> entity.</value>
    public virtual Portal? Portal { get; set; }

    /// <summary>
    /// Gets or sets the parent tab.
    /// </summary>
    /// <value>Navigation property to the parent <see cref="Tab"/>. May be null for root tabs.</value>
    public virtual Tab? ParentTab { get; set; }

    /// <summary>
    /// Gets or sets the collection of child tabs.
    /// </summary>
    /// <value>A collection of child <see cref="Tab"/> entities.</value>
    public virtual ICollection<Tab> ChildTabs { get; set; } = new List<Tab>();

    /// <summary>
    /// Gets or sets the collection of modules on this tab.
    /// </summary>
    /// <value>A collection of <see cref="Module"/> entities placed on this tab.</value>
    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();

    /// <summary>
    /// Gets or sets the collection of tab permissions.
    /// </summary>
    /// <value>A collection of <see cref="TabPermission"/> entities for this tab.</value>
    public virtual ICollection<TabPermission> TabPermissions { get; set; } = new List<TabPermission>();
}
