// MIGRATION: Update request DTO for the Tab (Page) feature, bound from the body of PUT /api/v1/tabs/{id}
// MIGRATION: (TabsController -> TabService.UpdateAsync). Carries the TabID identifier plus the client-writable
// MIGRATION: mutable fields. Projected from Library/Components/Tabs/TabInfo.vb writable field shapes.
// MIGRATION: MUST NOT expose IsDeleted (internal soft-delete state) or computed/derived fields
// MIGRATION: (Level, HasChildren, TabPath, TabType). Legacy Null.NullInteger -> int? (ParentId, RefreshInterval);
// MIGRATION: legacy Null.NullDate -> DateTime? (StartDate, EndDate).

namespace DnnMigration.Application.DTOs.Tab;

/// <summary>
/// Update request DTO for a Tab (Page). Carries the <see cref="TabID"/> identity of the tab being updated together
/// with the client-writable mutable fields. Server-managed soft-delete state (<c>IsDeleted</c>) and computed/derived
/// hierarchy fields (<c>Level</c>, <c>HasChildren</c>, <c>TabPath</c>, <c>TabType</c>) are intentionally omitted.
/// </summary>
public class UpdateTabDto
{
    /// <summary>Identity of the tab being updated.</summary>
    public int TabID { get; set; }

    /// <summary>Identifier of the owning portal.</summary>
    public int PortalID { get; set; }

    /// <summary>Display name of the tab/page.</summary>
    public string? TabName { get; set; }

    // MIGRATION: legacy Null.NullInteger sentinel -> nullable self-FK (null = root/top-level tab).
    /// <summary>Identifier of the parent tab, or <c>null</c> for a root-level tab.</summary>
    public int? ParentId { get; set; }

    /// <summary>Browser title override for the page.</summary>
    public string? Title { get; set; }

    /// <summary>Meta description for the page.</summary>
    public string? Description { get; set; }

    /// <summary>Meta keywords for the page.</summary>
    public string? KeyWords { get; set; }

    /// <summary>Whether the tab is visible in navigation.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Path to the icon file associated with the tab.</summary>
    public string? IconFile { get; set; }

    /// <summary>Optional URL the tab links to (used for external/redirect tabs).</summary>
    public string? Url { get; set; }

    /// <summary>Skin source applied to the page.</summary>
    public string? SkinSrc { get; set; }

    /// <summary>Container source applied to the page modules.</summary>
    public string? ContainerSrc { get; set; }

    // MIGRATION: legacy Null.NullDate sentinel -> nullable.
    /// <summary>Date from which the tab becomes active, or <c>null</c> if unbounded.</summary>
    public DateTime? StartDate { get; set; }

    // MIGRATION: legacy Null.NullDate sentinel -> nullable.
    /// <summary>Date after which the tab is no longer active, or <c>null</c> if unbounded.</summary>
    public DateTime? EndDate { get; set; }

    // MIGRATION: legacy Null.NullInteger sentinel -> nullable.
    /// <summary>Auto-refresh interval in seconds, or <c>null</c> when not configured.</summary>
    public int? RefreshInterval { get; set; }

    /// <summary>Whether the tab requires a secure (HTTPS) connection.</summary>
    public bool IsSecure { get; set; }

    /// <summary>Ordering position of the tab among its siblings.</summary>
    public int TabOrder { get; set; }
}
