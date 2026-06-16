namespace DnnMigration.Domain.Entities;

// MIGRATION: Local TabType enum inlined here (NOT in the Enums/ folder scope per AAP §0.3.1). Values are VERBATIM
// from TabInfo.vb L32-L38 (File=0, Normal=1, Tab=2, Url=3, Member=4 — VB implicit ordinals) to preserve behavioral
// equivalence: any persisted or compared integer value remains valid after the migration.
public enum TabType
{
    File = 0,
    Normal = 1,
    Tab = 2,
    Url = 3,
    Member = 4
}

// MIGRATION: Pure POCO ported from TabInfo.vb (legacy namespace DotNetNuke.Entities.Tabs; a DNN "Tab" == a site
// Page). Conversion rules applied: VB Property Get/Set -> C# auto-properties; VB Date -> DateTime; the legacy
// `Info` suffix dropped (TabInfo -> Tab). ZERO framework dependencies: XML serialization attributes
// (<XmlRoot>/<XmlElement>/<XmlArray>/<XmlIgnore>) and the `Implements IPropertyAccess` concern (GetProperty /
// Cacheability) are intentionally dropped. The IsDeleted soft-delete flag is PRESERVED. Null sentinel-defaulted
// members from the legacy constructor become nullable (ParentId int?, StartDate/EndDate DateTime?, optional
// strings string?); PortalID stays a non-nullable required FK int (consistent with the other entities). The
// runtime/render-only <XmlIgnore> members (SkinPath, ContainerPath, BreadCrumbs, Panes, Modules, IsSuperTab,
// FullUrl) and the Clone() helper are dropped. IsAdminTab is dropped because its legacy implementation depends on
// out-of-scope PortalController.GetCurrentPortalSettings / DataCache / PortalSettings (and on the dropped
// IsSuperTab); admin-tab determination moves to the service layer (Application/Infrastructure) and is NOT modeled
// on this zero-dependency POCO. TabType is re-expressed as a computed read-only property via the ported
// Globals.GetURLType logic. EF Core persistence is configured by Fluent IEntityTypeConfiguration in the
// Infrastructure layer (TabConfiguration), so no data attributes appear here.
public class Tab
{
    public int TabID { get; set; }

    public int TabOrder { get; set; }

    public int PortalID { get; set; }

    public string? TabName { get; set; }

    public bool IsVisible { get; set; }

    // MIGRATION: legacy constructor defaulted _ParentId = Null.NullInteger; root tabs have no parent -> nullable self-FK.
    public int? ParentId { get; set; }

    public int Level { get; set; }

    public string? IconFile { get; set; }

    public bool DisableLink { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? KeyWords { get; set; }

    // MIGRATION: soft-delete flag PRESERVED (legacy <XmlElement("isdeleted")> Boolean property).
    public bool IsDeleted { get; set; }

    public string? Url { get; set; }

    public string? SkinSrc { get; set; }

    public string? ContainerSrc { get; set; }

    public string? TabPath { get; set; }

    // MIGRATION: legacy `As Date` defaulted to Null.NullDate (optional scheduling window) -> nullable DateTime.
    public DateTime? StartDate { get; set; }

    // MIGRATION: legacy `As Date` defaulted to Null.NullDate (optional scheduling window) -> nullable DateTime.
    public DateTime? EndDate { get; set; }

    public bool HasChildren { get; set; }

    public int RefreshInterval { get; set; }

    public string? PageHeadText { get; set; }

    public bool IsSecure { get; set; }

    public string? AuthorizedRoles { get; set; }

    public string? AdministratorRoles { get; set; }

    // MIGRATION: legacy Security.Permissions.TabPermissionCollection -> EF Core navigation collection. The owning
    // ICollection<TabPermission> lives here (TabPermission.cs is the sibling entity, same namespace).
    public ICollection<TabPermission> TabPermissions { get; set; } = new List<TabPermission>();

    // MIGRATION: legacy read-only TabInfo.TabType => Globals.GetURLType(_Url) (Globals.vb L1914-L1934). Ported
    // verbatim; the original is a pure string parse with no external dependencies, so it is portable and lives on
    // the POCO. Conversion notes: (1) VB IsNumeric is approximated by int.TryParse — in the tab-id context the URL
    // override is always an integer tab id; (2) the legacy VB used the non-short-circuit `And` operator, converted
    // to `&&` here — every operand is a side-effect-free string test, so the evaluated result is identical;
    // (3) this is the intentional C# "Color Color" pattern (a property named TabType whose type is the enum
    // TabType) which compiles cleanly and warning-free because the enum members referenced inside are static.
    public TabType TabType
    {
        get
        {
            var url = Url ?? string.Empty;
            if (url.Length == 0)
            {
                return TabType.Normal;
            }

            if (!url.ToLower().StartsWith("mailto:")
                && url.IndexOf("://") == -1
                && !url.StartsWith("~")
                && !url.StartsWith("\\")
                && !url.StartsWith("/"))
            {
                if (int.TryParse(url, out _))
                {
                    return TabType.Tab;
                }

                if (url.ToLower().StartsWith("userid="))
                {
                    return TabType.Member;
                }

                return TabType.File;
            }

            return TabType.Url;
        }
    }
}
