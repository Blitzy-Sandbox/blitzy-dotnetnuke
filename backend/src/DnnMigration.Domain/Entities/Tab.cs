namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Tabs.TabInfo (Library/Components/Tabs/TabInfo.vb).
// Renamed TabInfo -> Tab (a "Tab" is a content page in DNN); XML attributes and IPropertyAccess removed;
// persistence-ignorant POCO. Portal-scoped (multi-tenant).
// MIGRATION: Dropped legacy runtime/presentation members loaded in PortalSettings (SkinPath, ContainerPath,
// BreadCrumbs, Panes, Modules, IsSuperTab, _SuperTabIdSet), the computed read-only props (TabType, FullUrl,
// IsAdminTab) and their inline TabType enum, and the methods Clone/GetURLType/NavigateURL/LinkClick plus the
// IPropertyAccess implementation (GetProperty/Cacheability). These are presentation/navigation concerns now
// handled by the Angular SPA / Application layer.
public class Tab
{
    public int TabId { get; set; }

    public int TabOrder { get; set; }

    // MIGRATION: Multi-tenant discriminator. Legacy PortalID initialized to Null.NullInteger (-1) -> nullable int.
    public int? PortalId { get; set; }

    public string? TabName { get; set; }

    public bool IsVisible { get; set; }

    // MIGRATION: Legacy ParentId initialized to Null.NullInteger (-1) -> nullable int (null = root page).
    public int? ParentId { get; set; }

    // MIGRATION: Legacy Level was <XmlIgnore> but is a plain scalar; kept as a persistence-ignorant property
    // (Infrastructure IEntityTypeConfiguration<Tab> decides whether to map or ignore it).
    public int Level { get; set; }

    public string? IconFile { get; set; }

    public bool DisableLink { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? KeyWords { get; set; }

    public bool IsDeleted { get; set; }

    public string? Url { get; set; }

    public string? SkinSrc { get; set; }

    public string? ContainerSrc { get; set; }

    public string? TabPath { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool HasChildren { get; set; }

    // MIGRATION: Legacy RefreshInterval initialized to Null.NullInteger (-1) -> nullable int.
    public int? RefreshInterval { get; set; }

    public string? PageHeadText { get; set; }

    public bool IsSecure { get; set; }

    public string? AuthorizedRoles { get; set; }

    public string? AdministratorRoles { get; set; }

    // MIGRATION: Replaces legacy TabPermissionCollection. Tab is secured by its tab permissions.
    public ICollection<TabPermission> TabPermissions { get; set; } = new List<TabPermission>();
}
