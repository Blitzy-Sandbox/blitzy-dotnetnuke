// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Tabs.TabInfo → C# 12 Tab entity
// Source: Library/Components/Tabs/TabInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
//   'DnnMigration.Domain.Entities'
// - Removed IPropertyAccess implementation (not needed for API/EF Core)
// - Converted Private fields (_TabID, _TabName, etc.) to C# auto-properties
// - Removed XmlRoot and XmlElement attributes (EF Core uses Fluent API)
// - Converted VB Date to C# DateTime? for StartDate/EndDate
// - Applied nullable reference types throughout
// - Removed runtime-only properties (SkinPath, ContainerPath, BreadCrumbs, Panes,
//   Modules ArrayList, IsSuperTab) - these are for runtime/UI rendering
// - Removed TabType enum definition (moved to Enums folder if needed)
// - Added navigation property for Portal
// - Added self-referencing navigation for parent Tab and child Tabs
// - Added navigation collection for Modules (ICollection<Module>)
// - Added navigation collection for TabPermissions (ICollection<TabPermission>)
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
/// multiple <see cref="Module"/> instances. Each tab belongs to a specific
/// <see cref="Portal"/> in the multi-tenant architecture.
/// </para>
/// <para>
/// The tab hierarchy supports unlimited nesting levels, where root tabs have a null
/// ParentId and child tabs reference their parent via the Parent navigation property.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Tabs.TabInfo.
/// Runtime-only properties (BreadCrumbs, Panes, SkinPath, ContainerPath, IsSuperTab)
/// have been removed - these are computed at runtime by services.
/// </para>
/// </remarks>
public class Tab
{
    /// <summary>
    /// Gets or sets the unique identifier for the tab.
    /// </summary>
    /// <value>The primary key of the tab record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TabID Private field.
    /// </remarks>
    public int TabId { get; set; }

    /// <summary>
    /// Gets or sets the order of the tab within its level.
    /// </summary>
    /// <value>The display order among sibling tabs. Lower values appear first.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TabOrder Private field.
    /// </remarks>
    public int TabOrder { get; set; }

    /// <summary>
    /// Gets or sets the portal identifier that this tab belongs to.
    /// </summary>
    /// <value>The foreign key to the Portal entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _PortalID Private field.
    /// In the original system, PortalID == NullInteger indicated a super/host tab.
    /// </remarks>
    public int PortalId { get; set; }

    /// <summary>
    /// Gets or sets the name of the tab.
    /// </summary>
    /// <value>The display name for the tab in navigation menus.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TabName Private field.
    /// </remarks>
    public string TabName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authorized roles for viewing this tab.
    /// </summary>
    /// <value>A semicolon-separated list of role IDs authorized to view this tab. May be null.</value>
    /// <remarks>
    /// MIGRATION: Legacy property for basic authorization. Consider using TabPermissions
    /// for more granular permission control. Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? AuthorizedRoles { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab is visible in navigation.
    /// </summary>
    /// <value><c>true</c> if the tab is visible in navigation menus; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IsVisible Private field.
    /// </remarks>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Gets or sets the parent tab identifier.
    /// </summary>
    /// <value>The foreign key to the parent Tab, or null for root-level tabs.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _ParentId Private field.
    /// Original VB.NET initialized to Null.NullInteger, converted to nullable int.
    /// </remarks>
    public int? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the nesting level of the tab in the hierarchy.
    /// </summary>
    /// <value>0 for root tabs, incrementing for each level of nesting.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Level Private field.
    /// </remarks>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the icon file path.
    /// </summary>
    /// <value>The relative path to the tab's icon file. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IconFile Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? IconFile { get; set; }

    /// <summary>
    /// Gets or sets the administrator roles for managing this tab.
    /// </summary>
    /// <value>A semicolon-separated list of role IDs with administrative access. May be null.</value>
    /// <remarks>
    /// MIGRATION: Legacy property for basic authorization. Consider using TabPermissions
    /// for more granular permission control. Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? AdministratorRoles { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab link is disabled.
    /// </summary>
    /// <value><c>true</c> if the link is disabled (container/parent only); otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _DisableLink Private field.
    /// When true, the tab serves as a container/grouping node without a clickable link.
    /// </remarks>
    public bool DisableLink { get; set; }

    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    /// <value>The HTML title for the page (appears in browser tab). May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Title Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the page description.
    /// </summary>
    /// <value>The meta description for SEO purposes. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Description Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the page keywords.
    /// </summary>
    /// <value>The meta keywords for SEO purposes. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _KeyWords Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? KeyWords { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab is deleted (soft delete).
    /// </summary>
    /// <value><c>true</c> if the tab is soft-deleted; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IsDeleted Private field.
    /// Soft-deleted tabs are retained in the database but not displayed.
    /// </remarks>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the URL for external links or redirects.
    /// </summary>
    /// <value>
    /// The URL to redirect to. May be null for normal tabs.
    /// Can contain file IDs, tab IDs, or external URLs.
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Url Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// The URL format determines the TabType (Normal, Tab, File, Url, Member).
    /// </remarks>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the skin source path.
    /// </summary>
    /// <value>The path to the skin template file. May be null for default skin.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _SkinSrc Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? SkinSrc { get; set; }

    /// <summary>
    /// Gets or sets the container source path.
    /// </summary>
    /// <value>The path to the container template file. May be null for default container.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _ContainerSrc Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? ContainerSrc { get; set; }

    /// <summary>
    /// Gets or sets the tab path in the hierarchy.
    /// </summary>
    /// <value>
    /// The hierarchical path of the tab using double-slash separators
    /// (e.g., "//Home//About//Team"). May be null.
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TabPath Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// </remarks>
    public string? TabPath { get; set; }

    /// <summary>
    /// Gets or sets the start date for tab visibility.
    /// </summary>
    /// <value>The date when the tab becomes visible. May be null for always visible.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _StartDate Private field (VB Date type).
    /// Original VB.NET initialized to Null.NullDate.
    /// </remarks>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for tab visibility.
    /// </summary>
    /// <value>The date when the tab becomes hidden. May be null for never ending.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _EndDate Private field (VB Date type).
    /// Original VB.NET initialized to Null.NullDate.
    /// </remarks>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab has child tabs.
    /// </summary>
    /// <value><c>true</c> if the tab has children; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _HasChildren Private field.
    /// This is a denormalized flag for performance optimization.
    /// </remarks>
    public bool HasChildren { get; set; }

    /// <summary>
    /// Gets or sets the refresh interval for the page in seconds.
    /// </summary>
    /// <value>The meta refresh interval in seconds, or null for no auto-refresh.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _RefreshInterval Private field.
    /// Original VB.NET initialized to Null.NullInteger.
    /// </remarks>
    public int? RefreshInterval { get; set; }

    /// <summary>
    /// Gets or sets custom HTML to include in the page head.
    /// </summary>
    /// <value>Custom HTML content for the head section. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _PageHeadText Private field.
    /// Original VB.NET initialized to Null.NullString.
    /// Use with caution - content is injected directly into the page head.
    /// </remarks>
    public string? PageHeadText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab requires secure (HTTPS) access.
    /// </summary>
    /// <value><c>true</c> if HTTPS is required; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IsSecure Private field.
    /// </remarks>
    public bool IsSecure { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the portal that this tab belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Portal"/> entity.</value>
    /// <remarks>
    /// This property enables navigation from a tab to its parent portal.
    /// The relationship is defined by the PortalId foreign key.
    /// </remarks>
    public virtual Portal? Portal { get; set; }

    /// <summary>
    /// Gets or sets the parent tab in the hierarchy.
    /// </summary>
    /// <value>Navigation property to the parent <see cref="Tab"/>. Null for root-level tabs.</value>
    /// <remarks>
    /// This self-referencing relationship enables hierarchical navigation.
    /// The relationship is defined by the ParentId foreign key.
    /// </remarks>
    public virtual Tab? Parent { get; set; }

    /// <summary>
    /// Gets or sets the collection of child tabs.
    /// </summary>
    /// <value>A collection of child <see cref="Tab"/> entities.</value>
    /// <remarks>
    /// This is the inverse navigation property for the Parent relationship.
    /// Enables querying child tabs and building navigation trees.
    /// </remarks>
    public virtual ICollection<Tab> Children { get; set; } = new List<Tab>();

    /// <summary>
    /// Gets or sets the collection of modules on this tab.
    /// </summary>
    /// <value>A collection of <see cref="Module"/> entities placed on this tab.</value>
    /// <remarks>
    /// MIGRATION: This replaces the VB.NET _Modules ArrayList property.
    /// The original ArrayList was runtime-only; this is a proper EF Core navigation.
    /// </remarks>
    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();

    /// <summary>
    /// Gets or sets the collection of tab permissions.
    /// </summary>
    /// <value>A collection of <see cref="TabPermission"/> entities for this tab.</value>
    /// <remarks>
    /// MIGRATION: This replaces the VB.NET TabPermissionCollection property type.
    /// Provides granular permission control beyond AuthorizedRoles/AdministratorRoles.
    /// </remarks>
    public virtual ICollection<TabPermission> TabPermissions { get; set; } = new List<TabPermission>();
}
