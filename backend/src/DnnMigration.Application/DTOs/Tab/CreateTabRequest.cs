namespace DnnMigration.Application.DTOs.Tab;

// MIGRATION: Inbound request DTO to create a Tab (DNN page). Derived from the Domain entity
// DnnMigration.Domain.Entities.Tab (legacy DotNetNuke.Entities.Tabs.TabInfo, Library/Components/Tabs/TabInfo.vb).
// Mutable plain shape bound from the JSON request body by ASP.NET Core model binding — no business logic, no
// data access, no validation attributes (FluentValidation CreateTabValidator enforces required/length rules).
// MIGRATION: Server-generated TabId is OMITTED (assigned by the data store on insert). Computed hierarchy fields
// Level, TabPath and HasChildren are OMITTED (derived server-side from ParentId + the page tree, not client-supplied).
// PortalId is the multi-tenant discriminator (nullable, mirroring the entity's legacy Null.NullInteger sentinel);
// the service enforces tenant scoping. ParentId/RefreshInterval are nullable int (legacy Null.NullInteger).
public class CreateTabRequest
{
    // MIGRATION: Multi-tenant discriminator (target portal for the new page).
    public int? PortalId { get; set; }

    public string? TabName { get; set; }

    // MIGRATION: Parent page id; null = create as a root page (legacy Null.NullInteger).
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

    // MIGRATION: CP1 review (TabService #2, CRITICAL) — tab-permission grants supplied on create. Legacy
    // TabController.AddTab (L336-349) iterated the TabPermissionCollection and persisted each row whose AllowAccess
    // was true (AddTabPermission). TabService applies that AllowAccess filter. Defaults to an empty list so a create
    // with no permissions is valid (the legacy `If Not objTab.TabPermissions Is Nothing` guard).
    public List<TabPermissionDto> Permissions { get; set; } = new();
}
