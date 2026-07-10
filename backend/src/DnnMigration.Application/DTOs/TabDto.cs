namespace DnnMigration.Application.DTOs;

/// <summary>
/// Read-only projection of a portal tab (page) exposed by the API.
/// Returned by <c>GET /api/tabs</c> and <c>GET /api/tabs/{id}</c> and projected
/// from the <c>Tab</c> domain entity via AutoMapper.
/// </summary>
/// <remarks>
/// MIGRATION: mapped from the legacy VB.NET <c>TabInfo</c> class
/// (Library/Components/Tabs/TabInfo.vb). This is a pure data-transfer record;
/// it carries no behavior, no data access, and no serialization attributes.
/// Legacy, non-AAP members of <c>TabInfo</c> (AuthorizedRoles,
/// AdministratorRoles, IsDeleted, the TabPermissions collection, and the
/// runtime-only PortalSettings helpers such as SkinPath/BreadCrumbs/Modules)
/// are intentionally excluded from the API surface.
/// </remarks>
public record TabDto
{
    /// <summary>Unique identifier of the tab (page).</summary>
    public int TabID { get; init; }

    /// <summary>Display/sort order of the tab relative to its siblings.</summary>
    public int TabOrder { get; init; }

    /// <summary>Identifier of the portal that owns this tab.</summary>
    public int PortalID { get; init; }

    /// <summary>Name of the tab as shown in navigation.</summary>
    public string TabName { get; init; } = string.Empty;

    /// <summary>Whether the tab is visible in the portal navigation.</summary>
    public bool IsVisible { get; init; }

    /// <summary>Identifier of the parent tab, establishing the page hierarchy.</summary>
    public int ParentId { get; init; }

    /// <summary>Depth of the tab within the hierarchy (root tabs are level 0).</summary>
    public int Level { get; init; }

    /// <summary>Relative path to the icon file associated with the tab.</summary>
    public string IconFile { get; init; } = string.Empty;

    /// <summary>Whether the tab acts as a non-clickable heading (link disabled).</summary>
    public bool DisableLink { get; init; }

    /// <summary>Browser/page title for the tab.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Meta description associated with the page.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Meta keywords associated with the page.</summary>
    public string KeyWords { get; init; } = string.Empty;

    /// <summary>Target URL for URL/redirect-type tabs.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Relative path to the skin (layout) applied to the tab.</summary>
    public string SkinSrc { get; init; } = string.Empty;

    /// <summary>Relative path to the container applied to the tab's modules.</summary>
    public string ContainerSrc { get; init; } = string.Empty;

    /// <summary>Hierarchical path string that uniquely locates the tab within the portal.</summary>
    public string TabPath { get; init; } = string.Empty;

    /// <summary>Date from which the tab becomes published/active.</summary>
    public DateTime StartDate { get; init; }

    /// <summary>Date after which the tab is no longer published/active.</summary>
    public DateTime EndDate { get; init; }

    /// <summary>Auto-refresh interval, in seconds, for the rendered page.</summary>
    public int RefreshInterval { get; init; }

    /// <summary>Custom markup injected into the page's &lt;head&gt; section.</summary>
    public string PageHeadText { get; init; } = string.Empty;

    /// <summary>Whether the tab requires a secure (SSL) connection.</summary>
    public bool IsSecure { get; init; }

    /// <summary>Whether the tab has one or more child tabs.</summary>
    public bool HasChildren { get; init; }
}
