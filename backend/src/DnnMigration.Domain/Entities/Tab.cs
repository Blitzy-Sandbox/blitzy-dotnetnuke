namespace DnnMigration.Domain.Entities;

// MIGRATION: Local TabType enum inlined here (NOT in the Enums/ folder scope per AAP §0.3.1).
// Values are VERBATIM from TabInfo.vb L32-L38 (implicit VB ordinals): File=0, Normal=1, Tab=2,
// Url=3, Member=4. The ordinals are written explicitly to lock in behavioral equivalence so that
// any persisted or compared integer values remain valid after the migration.
public enum TabType
{
    File = 0,
    Normal = 1,
    Tab = 2,
    Url = 3,
    Member = 4
}

// MIGRATION: Pure POCO Tab aggregate-root entity (a DNN "Tab" == a site Page), ported from
// TabInfo.vb (legacy namespace DotNetNuke.Entities.Tabs). Public-contract / domain semantics are
// preserved per AAP §0.7.1.
//
// Decisions captured for the Minimal Change Clause (MIGRATION_NOTES.md):
//  * XML serialization attributes (<XmlRoot("tab")>, <XmlElement(...)>, <XmlIgnore()>,
//    <XmlArray(...)>) are intentionally dropped; relational mapping is handled by a Fluent
//    IEntityTypeConfiguration<Tab> in the Infrastructure layer, honoring ADR-002 (the existing DNN
//    4.9.0.85 schema is mapped UNCHANGED — no table/column changes, no EF migrations).
//  * "Implements IPropertyAccess" plus its GetProperty/Cacheability members are dropped: token
//    property access is not a Domain concern and would pull in out-of-scope DotNetNuke.Services.Tokens.
//  * The legacy constructor Null.* sentinels are dropped in favor of CLR defaults and nullable
//    reference types: ParentId (Null.NullInteger) -> int?; StartDate/EndDate (As Date optional
//    windows) -> DateTime?; optional display strings -> string?. PortalID is a required FK and stays
//    a non-nullable int, consistent with every other entity in this layer.
//  * The IsDeleted soft-delete flag is PRESERVED.
//  * Runtime/render-only members (SkinPath, ContainerPath, BreadCrumbs, Panes, Modules, IsSuperTab,
//    FullUrl, Clone) are dropped — they are presentation/runtime hydration concerns, not persisted
//    domain state.
//  * IsAdminTab is dropped: its legacy implementation depends on out-of-scope
//    PortalController.GetCurrentPortalSettings, DataCache, PortalSettings, and the dropped IsSuperTab.
//    Admin-tab determination moves to the service layer (Application/Infrastructure); this POCO takes
//    NO dependency on PortalController/DataCache/PortalSettings.
//
// Kept as a plain, attribute-free POCO with zero framework dependencies (Clean Architecture inner
// ring). All members are covered by ImplicitUsings (System, System.Collections.Generic), so the file
// declares no `using` directives; TabPermission lives in this same namespace.
public class Tab
{
    // VB: <XmlElement("tabid")> Public Property TabID() As Integer — identity scalar / primary key.
    public int TabID { get; set; }

    // VB: <XmlElement("taborder")> Public Property TabOrder() As Integer — ordering within siblings.
    public int TabOrder { get; set; }

    // VB: <XmlElement("portalid")> Public Property PortalID() As Integer.
    // MIGRATION: legacy constructor seeded Null.NullInteger, but PortalID is a required FK; modeled as
    // a non-nullable int for consistency with all other entities (host/super tabs are handled at the
    // service layer, not by nulling the FK on the POCO).
    public int PortalID { get; set; }

    // VB: <XmlElement("name")> Public Property TabName() As String — optional display name.
    public string? TabName { get; set; }

    // VB: <XmlElement("visible")> Public Property IsVisible() As Boolean — menu visibility flag.
    public bool IsVisible { get; set; }

    // VB: <XmlElement("parentid")> Public Property ParentId() As Integer.
    // MIGRATION: legacy default Null.NullInteger; root tabs have no parent -> nullable self-FK (int?).
    public int? ParentId { get; set; }

    // VB: <XmlIgnore()> Public Property Level() As Integer — depth in the tab hierarchy.
    public int Level { get; set; }

    // VB: <XmlElement("iconfile")> Public Property IconFile() As String — optional icon path.
    public string? IconFile { get; set; }

    // VB: <XmlElement("disabled")> Public Property DisableLink() As Boolean — render as non-clickable.
    public bool DisableLink { get; set; }

    // VB: <XmlElement("title")> Public Property Title() As String — optional page title.
    public string? Title { get; set; }

    // VB: <XmlElement("description")> Public Property Description() As String — optional meta description.
    public string? Description { get; set; }

    // VB: <XmlElement("keywords")> Public Property KeyWords() As String — optional meta keywords.
    public string? KeyWords { get; set; }

    // VB: <XmlElement("isdeleted")> Public Property IsDeleted() As Boolean.
    // MIGRATION: soft-delete flag PRESERVED (recycle-bin semantics retained; list reads filter on it).
    public bool IsDeleted { get; set; }

    // VB: <XmlElement("url")> Public Property Url() As String — optional URL (drives TabType below).
    public string? Url { get; set; }

    // VB: <XmlElement("skinsrc")> Public Property SkinSrc() As String — optional skin source path.
    public string? SkinSrc { get; set; }

    // VB: <XmlElement("containersrc")> Public Property ContainerSrc() As String — optional container path.
    public string? ContainerSrc { get; set; }

    // VB: <XmlElement("tabpath")> Public Property TabPath() As String — computed hierarchical path string.
    public string? TabPath { get; set; }

    // VB: <XmlElement("startdate")> Public Property StartDate() As Date.
    // MIGRATION: VB Date -> C# DateTime; legacy Null.NullDate optional window -> DateTime?.
    public DateTime? StartDate { get; set; }

    // VB: <XmlElement("enddate")> Public Property EndDate() As Date.
    // MIGRATION: VB Date -> C# DateTime; legacy Null.NullDate optional window -> DateTime?.
    public DateTime? EndDate { get; set; }

    // VB: <XmlElement("haschildren")> Public Property HasChildren() As Boolean — has child tabs.
    public bool HasChildren { get; set; }

    // VB: <XmlElement("refreshinterval")> Public Property RefreshInterval() As Integer.
    // MIGRATION: legacy default Null.NullInteger; treated as a non-nullable int (0 == no refresh),
    // matching how the legacy reader coalesced the column. Kept simple for the persisted scalar.
    public int RefreshInterval { get; set; }

    // VB: <XmlElement("pageheadtext")> Public Property PageHeadText() As String — optional <head> markup.
    public string? PageHeadText { get; set; }

    // VB: <XmlElement("issecure")> Public Property IsSecure() As Boolean — require HTTPS for this tab.
    public bool IsSecure { get; set; }

    // VB: <XmlElement("authorizedroles")> Public Property AuthorizedRoles() As String — optional CSV of roles.
    public string? AuthorizedRoles { get; set; }

    // VB: <XmlElement("administratorroles")> Public Property AdministratorRoles() As String — optional CSV of roles.
    public string? AdministratorRoles { get; set; }

    // MIGRATION: legacy TabPermissionCollection (Security.Permissions.TabPermissionCollection) ->
    // EF Core navigation collection. Initialized to an empty List<TabPermission> so newly constructed
    // Tab instances expose a non-null collection. TabPermission.cs already created in this namespace.
    public ICollection<TabPermission> TabPermissions { get; set; } = new List<TabPermission>();

    // MIGRATION: legacy TabInfo.TabType (ReadOnly, L406-L410) delegated to Globals.GetURLType(_Url)
    // (Globals.vb L1914-L1934). Ported verbatim here as a computed read-only property — it is a pure
    // string parse with zero external dependencies, so it is portable into the Domain layer.
    // Notes:
    //  (1) VB IsNumeric(url) is approximated by int.TryParse: a numeric Url in this context is always a
    //      target tab id (an integer), so int.TryParse reproduces the intended classification.
    //  (2) VB used the non-short-circuit And operator; converted to && here. The operands have no side
    //      effects, so short-circuiting yields an identical result.
    //  (3) This is the intentional C# "Color Color" pattern — a property named TabType whose type is
    //      the enum TabType. It compiles cleanly and warning-free because the enum members referenced
    //      inside the getter are static, so there is no ambiguity between the property and the type.
    public TabType TabType
    {
        get
        {
            var url = Url ?? string.Empty;
            if (url.Length == 0)
            {
                // Empty Url => a standard navigable page.
                return TabType.Normal;
            }

            // Not an email link, not an absolute scheme (no "://"), and not an app-relative/rooted/UNC path.
            if (!url.ToLower().StartsWith("mailto:")
                && url.IndexOf("://") == -1
                && !url.StartsWith("~")
                && !url.StartsWith("\\")
                && !url.StartsWith("/"))
            {
                if (int.TryParse(url, out _))
                {
                    // Numeric => redirect to another tab id.
                    return TabType.Tab;
                }

                if (url.ToLower().StartsWith("userid="))
                {
                    // "userid=" => a member profile link.
                    return TabType.Member;
                }

                // Otherwise it is a file/document reference.
                return TabType.File;
            }

            // Everything else (mailto:, scheme://, ~/, \\, /) => an external/explicit URL.
            return TabType.Url;
        }
    }
}
