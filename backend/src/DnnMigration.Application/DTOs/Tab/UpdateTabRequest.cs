namespace DnnMigration.Application.DTOs.Tab;

// MIGRATION: Inbound request DTO to update an existing Tab (DNN page). Derived from the Domain entity
// DnnMigration.Domain.Entities.Tab (legacy DotNetNuke.Entities.Tabs.TabInfo, Library/Components/Tabs/TabInfo.vb).
// Mutable plain shape bound from the JSON request body — no business logic, no data access, no validation attributes
// (FluentValidation UpdateTabValidator enforces rules).
// MIGRATION: TabId is route-bound (taken from the /api/tabs/{id} URL, not the body) and is therefore OMITTED here.
// PortalId is OMITTED because the tenant is immutable on update — the service preserves it from the existing entity.
// Computed hierarchy fields Level, TabPath and HasChildren are OMITTED (derived server-side). ParentId/RefreshInterval
// remain nullable int (legacy Null.NullInteger sentinels); StartDate/EndDate are nullable DateTime (legacy Null.NullDate).
public class UpdateTabRequest
{
    public string? TabName { get; set; }

    // MIGRATION: Parent page id; null = root page (legacy Null.NullInteger). Changing it reparents the page.
    public int? ParentId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? KeyWords { get; set; }

    public bool IsVisible { get; set; }

    public bool DisableLink { get; set; }

    public string? Url { get; set; }

    public string? IconFile { get; set; }

    public string? SkinSrc { get; set; }

    public string? ContainerSrc { get; set; }

    public bool IsSecure { get; set; }

    // MIGRATION: Nullable (legacy Null.NullInteger sentinel).
    public int? RefreshInterval { get; set; }

    public string? PageHeadText { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int TabOrder { get; set; }
}
