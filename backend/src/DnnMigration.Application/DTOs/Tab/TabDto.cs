using DnnMigration.Domain.Entities;

namespace DnnMigration.Application.DTOs.Tab;

// MIGRATION: Read projection of TabInfo.vb (DotNetNuke.Entities.Tabs). Plain POCO anti-corruption DTO for the
// Tab (Page) feature, returned by TabsController GET endpoints (/api/v1/tabs). XML serialization attributes and
// IPropertyAccess are dropped. Legacy Null.NullInteger sentinels -> int? (ParentId, RefreshInterval); legacy Date
// -> DateTime? (StartDate, EndDate). TabType REUSES the verbatim Domain enum (DnnMigration.Domain.Entities.TabType)
// and is NOT redefined here. EF navigation collections (TabPermissions/BreadCrumbs/Panes/Modules) are intentionally
// not exposed.
public class TabDto
{
    public int TabID { get; set; }
    public int TabOrder { get; set; }
    public int PortalID { get; set; }
    public string? TabName { get; set; }
    public bool IsVisible { get; set; }

    // MIGRATION: legacy Null.NullInteger sentinel -> nullable self-FK (root tabs have no parent).
    public int? ParentId { get; set; }

    public int Level { get; set; }
    public string? IconFile { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? KeyWords { get; set; }
    public string? Url { get; set; }
    public string? SkinSrc { get; set; }
    public string? ContainerSrc { get; set; }
    public string? TabPath { get; set; }

    // MIGRATION: legacy Null.NullDate sentinels -> nullable.
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool HasChildren { get; set; }

    // MIGRATION: legacy Null.NullInteger sentinel -> nullable.
    public int? RefreshInterval { get; set; }

    public bool IsSecure { get; set; }

    // MIGRATION: reuses the verbatim Domain TabType enum (File/Normal/Tab/Url/Member); legacy computed via
    // Globals.GetURLType(Url). Not redefined in the DTO layer.
    public TabType TabType { get; set; }
}
