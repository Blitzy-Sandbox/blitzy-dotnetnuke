using DnnMigration.Domain.Entities;

namespace DnnMigration.Application.DTOs.Tab;

// MIGRATION: Read projection of TabInfo.vb (DotNetNuke.Entities.Tabs; a DNN "Tab" == a site Page). Plain POCO
// anti-corruption boundary between the Tab domain entity and the REST API, returned by TabsController GET
// endpoints (/api/v1/tabs and /api/v1/tabs/{id}). Authored as a POCO (not a record) for AutoMapper +
// System.Text.Json compatibility. Legacy XML serialization attributes (<XmlRoot>/<XmlElement>/<XmlArray>/
// <XmlIgnore>) and the IPropertyAccess concern are dropped. Legacy Null.NullInteger sentinels -> int?
// (ParentId, RefreshInterval); legacy Null.NullDate -> DateTime? (StartDate, EndDate). TabType REUSES the
// verbatim Domain enum (DnnMigration.Domain.Entities.TabType: File/Normal/Tab/Url/Member) and is NOT redefined
// here. EF navigation collections (TabPermissions/BreadCrumbs/Panes/Modules) and runtime/render-only or
// out-of-scope members (DisableLink, IsDeleted, PageHeadText, AuthorizedRoles, AdministratorRoles, IsSuperTab,
// IsAdminTab, FullUrl, SkinPath, ContainerPath) are intentionally NOT exposed on this read DTO.

/// <summary>
/// Full read projection for a Tab (Page) resource. This is the response shape returned by
/// <c>TabsController</c> for <c>GET /api/v1/tabs</c> and <c>GET /api/v1/tabs/{id}</c>, forming the read side of
/// the anti-corruption boundary between the <c>Tab</c> domain entity and the REST API. AutoMapper maps
/// <c>Tab</c> -&gt; <see cref="TabDto"/> via the sibling <c>TabProfile</c>; the property names and types here
/// mirror the <c>Tab</c> entity so the mapping requires no per-member configuration (the entity's plain
/// <c>int RefreshInterval</c> maps implicitly to this DTO's nullable <c>int?</c>).
/// </summary>
public class TabDto
{
    /// <summary>Primary key of the tab (TabID).</summary>
    public int TabID { get; set; }

    /// <summary>Ordering position of the tab among its siblings.</summary>
    public int TabOrder { get; set; }

    /// <summary>Identifier of the portal that owns this tab.</summary>
    public int PortalID { get; set; }

    /// <summary>Display name of the tab/page. Null when unset.</summary>
    public string? TabName { get; set; }

    /// <summary>Whether the tab is visible in site navigation.</summary>
    public bool IsVisible { get; set; }

    // MIGRATION: legacy Null.NullInteger sentinel -> nullable self-FK (null = root/top-level tab).
    /// <summary>Identifier of the parent tab, or <c>null</c> for a root-level tab.</summary>
    public int? ParentId { get; set; }

    /// <summary>Depth of the tab within the page hierarchy.</summary>
    public int Level { get; set; }

    /// <summary>Path to the icon file associated with the tab. Null when unset.</summary>
    public string? IconFile { get; set; }

    /// <summary>Browser title override for the page. Null when unset.</summary>
    public string? Title { get; set; }

    /// <summary>Meta description for the page. Null when unset.</summary>
    public string? Description { get; set; }

    /// <summary>Meta keywords for the page. Null when unset.</summary>
    public string? KeyWords { get; set; }

    /// <summary>Optional URL the tab links to (used for external/redirect tabs). Null when unset.</summary>
    public string? Url { get; set; }

    /// <summary>Skin source applied to the page. Null when unset.</summary>
    public string? SkinSrc { get; set; }

    /// <summary>Container source applied to the page modules. Null when unset.</summary>
    public string? ContainerSrc { get; set; }

    /// <summary>Materialized hierarchical path of the tab. Null when unset.</summary>
    public string? TabPath { get; set; }

    // MIGRATION: legacy Null.NullDate sentinel -> nullable.
    /// <summary>Date from which the tab becomes active, or <c>null</c> if unbounded.</summary>
    public DateTime? StartDate { get; set; }

    // MIGRATION: legacy Null.NullDate sentinel -> nullable.
    /// <summary>Date after which the tab is no longer active, or <c>null</c> if unbounded.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Whether the tab has child tabs in the hierarchy.</summary>
    public bool HasChildren { get; set; }

    // MIGRATION: legacy Null.NullInteger sentinel -> nullable (entity stores a plain int; AutoMapper int -> int?
    // MIGRATION: is implicit).
    /// <summary>Auto-refresh interval in seconds, or <c>null</c> when not configured.</summary>
    public int? RefreshInterval { get; set; }

    /// <summary>Whether the tab requires a secure (HTTPS) connection.</summary>
    public bool IsSecure { get; set; }

    // MIGRATION: reuses the verbatim Domain TabType enum (File/Normal/Tab/Url/Member); legacy computed via
    // MIGRATION: Globals.GetURLType(Url). Not redefined in the DTO layer. The property name matching its enum
    // MIGRATION: type (the C# "Color Color" pattern) is valid and warning-free here because the type is TabDto.
    /// <summary>Classification of the tab derived from its URL (File, Normal, Tab, Url, or Member).</summary>
    public TabType TabType { get; set; }
}
