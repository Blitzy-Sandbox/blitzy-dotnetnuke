namespace DnnMigration.Application.DTOs.Tab;

// MIGRATION: Read-model DTO projected from the Domain entity DnnMigration.Domain.Entities.Tab
// (legacy DotNetNuke.Entities.Tabs.TabInfo, Library/Components/Tabs/TabInfo.vb). A "Tab" is a DNN content page.
// Plain shape only — no business logic, no data access, no validation; the AutoMapper TabProfile maps Tab -> TabResponse
// and the API returns it inside the standard { data, meta } success envelope.
// MIGRATION: The entity navigation TabPermissions (ICollection<TabPermission>) is intentionally NOT exposed here —
// tab-permission management is out of the /api/tabs CRUD scope and returning the entity collection would over-fetch
// and leak persistence types (AAP 0.7.7: never return raw EF entities; DTO projection avoids over-fetching).
// MIGRATION: Hierarchy fields (ParentId, Level, TabOrder, HasChildren, TabPath) are retained so the Angular SPA can
// render the page tree. PortalId/ParentId/RefreshInterval are nullable int (legacy Null.NullInteger sentinels);
// StartDate/EndDate are nullable DateTime (legacy Null.NullDate).
public record TabResponse
{
    public int TabId { get; init; }

    public int TabOrder { get; init; }

    // MIGRATION: Multi-tenant discriminator. Nullable (legacy Null.NullInteger sentinel).
    public int? PortalId { get; init; }

    public string? TabName { get; init; }

    public bool IsVisible { get; init; }

    // MIGRATION: Nullable (legacy Null.NullInteger sentinel; null = root page).
    public int? ParentId { get; init; }

    public int Level { get; init; }

    public string? IconFile { get; init; }

    public bool DisableLink { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? KeyWords { get; init; }

    public bool IsDeleted { get; init; }

    public string? Url { get; init; }

    public string? SkinSrc { get; init; }

    public string? ContainerSrc { get; init; }

    public string? TabPath { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public bool HasChildren { get; init; }

    // MIGRATION: Nullable (legacy Null.NullInteger sentinel).
    public int? RefreshInterval { get; init; }

    public string? PageHeadText { get; init; }

    public bool IsSecure { get; init; }

    public string? AuthorizedRoles { get; init; }

    public string? AdministratorRoles { get; init; }
}
