namespace DnnMigration.Application.DTOs.Tab;

// MIGRATION: Update DTO for the Tab (Page) feature, bound from PUT /api/v1/tabs/{id} request bodies. Carries the
// TabID identifier plus client-writable mutable fields. MUST NOT expose IsDeleted (internal soft-delete state) or
// computed fields (Level, HasChildren, TabPath, TabType). Legacy Null.NullInteger -> int? (ParentId,
// RefreshInterval); legacy Date -> DateTime? (StartDate, EndDate).
public class UpdateTabDto
{
    public int TabID { get; set; }
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
