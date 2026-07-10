namespace DnnMigration.Domain.Entities;

/// <summary>
/// Tab (portal page) entity.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Tabs.TabInfo
// (Library/Components/Tabs/TabInfo.vb, L40, XmlRoot "tab"). The IPropertyAccess implementation
// (GetProperty/Cacheability) and the ArrayList render/runtime members (BreadCrumbs, Panes, Modules)
// plus other XmlIgnore computed/presentation properties are dropped; only persisted data/state and
// the structural Level/permissions are retained. VB `Date` -> `DateTime`.
public class Tab
{
    public int TabID { get; set; }
    public int TabOrder { get; set; }
    public int PortalID { get; set; }

    // MIGRATION: legacy XML element "name" (property name TabName differs from element name)
    public string TabName { get; set; } = string.Empty;

    // MIGRATION: legacy XML element "visible" (property name IsVisible differs from element name)
    public bool IsVisible { get; set; }

    public int ParentId { get; set; }

    // MIGRATION: legacy XmlIgnore structural depth; retained as plain state.
    public int Level { get; set; }

    // MIGRATION: The legacy Tabs table (DotNetNuke.Schema.SqlDataProvider) declares IconFile, Title,
    // Description, KeyWords, Url, SkinSrc, ContainerSrc and PageHeadText as NULLable columns. These
    // properties are therefore nullable reference types so EF Core maps them to NULLable columns (matching
    // the existing schema per AAP schema-fidelity) and does not treat them as required on SaveChanges. The
    // `= string.Empty` initializer is retained for in-memory-constructed defaults; EF derives column
    // nullability from the type annotation.
    public string? IconFile { get; set; } = string.Empty;

    // MIGRATION: legacy XML element "disabled" (property name DisableLink differs from element name)
    public bool DisableLink { get; set; }

    public string? Title { get; set; } = string.Empty;
    public string? Description { get; set; } = string.Empty;
    public string? KeyWords { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public string? Url { get; set; } = string.Empty;
    public string? SkinSrc { get; set; } = string.Empty;
    public string? ContainerSrc { get; set; } = string.Empty;
    public string TabPath { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // MIGRATION: legacy TabPermissionCollection -> ICollection<TabPermission>
    public ICollection<TabPermission> TabPermissions { get; set; } = new List<TabPermission>();

    public bool HasChildren { get; set; }
    public int RefreshInterval { get; set; }
    public string? PageHeadText { get; set; } = string.Empty;
    public bool IsSecure { get; set; }
    public string AuthorizedRoles { get; set; } = string.Empty;
    public string AdministratorRoles { get; set; } = string.Empty;
}
