// MIGRATION: Creation DTO for the Tab (Page) feature, bound from POST /api/v1/tabs request bodies
// (TabsController -> TabService.CreateAsync). C# projection of the writable subset of the legacy VB
// TabInfo class (Library/Components/Tabs/TabInfo.vb, Namespace DotNetNuke.Entities.Tabs).
// MIGRATION: Exposes ONLY client-writable creation fields. MUST NOT expose IsDeleted (internal
// soft-delete state) or computed/hierarchy-derived fields (Level, HasChildren, TabPath, TabType);
// TabID is server-generated identity and is omitted on create.
// MIGRATION: Legacy Null.NullInteger sentinels -> int? (ParentId, RefreshInterval); legacy Null.NullDate
// sentinels -> DateTime? (StartDate, EndDate). Property names/casing preserved verbatim so AutoMapper
// maps by-name with no extra configuration. Pure POCO; no attributes, no packages, no using directives.

namespace DnnMigration.Application.DTOs.Tab;

/// <summary>
/// Creation request payload for a Tab (Page). Carries only the client-writable fields accepted by
/// <c>POST /api/v1/tabs</c>; identity, computed/hierarchy, and soft-delete fields are intentionally excluded.
/// </summary>
public class CreateTabDto
{
    public int PortalID { get; set; }
    public string? TabName { get; set; }

    // MIGRATION: legacy Null.NullInteger sentinel -> nullable self-FK.
    public int? ParentId { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? KeyWords { get; set; }
    public bool IsVisible { get; set; }
    public string? IconFile { get; set; }
    public string? Url { get; set; }
    public string? SkinSrc { get; set; }
    public string? ContainerSrc { get; set; }

    // MIGRATION: legacy Null.NullDate sentinels -> nullable.
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // MIGRATION: legacy Null.NullInteger sentinel -> nullable.
    public int? RefreshInterval { get; set; }

    public bool IsSecure { get; set; }
    public int TabOrder { get; set; }
}
