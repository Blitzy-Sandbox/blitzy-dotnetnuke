namespace DnnMigration.Application.DTOs.Tab;

// MIGRATION: Creation DTO for the Tab (Page) feature, bound from POST /api/v1/tabs request bodies. Exposes only
// client-writable fields. MUST NOT expose IsDeleted (internal soft-delete state) or computed fields (Level,
// HasChildren, TabPath, TabType). Legacy Null.NullInteger -> int? (ParentId, RefreshInterval); legacy Date ->
// DateTime? (StartDate, EndDate). Source of truth: Library/Components/Tabs/TabInfo.vb (VB class TabInfo).
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
