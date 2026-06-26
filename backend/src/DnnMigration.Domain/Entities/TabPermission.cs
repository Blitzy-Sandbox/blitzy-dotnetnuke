namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.TabPermissionInfo
// (Library/Components/Security/Permissions/TabPermission.vb). Renamed to TabPermission;
// persistence-ignorant POCO. XML attributes removed.
//
// MIGRATION (QA-4 #4, CRITICAL): see ModulePermission for the full rationale. In the DNN SQL Server schema
// [TabPermission] is a PHYSICALLY SEPARATE table with its OWN identity PK [TabPermissionID] and a [PermissionID]
// FK to the [Permission] catalog (6 columns: TabPermissionID, TabID, PermissionID, RoleID, AllowAccess, UserID).
// The legacy "Inherits PermissionInfo" forced EF's TPC strategy to duplicate the base-Permission columns onto
// [TabPermission] -> "Invalid column name" on real SQL Server. FIX: COMPOSITION (HAS-A), not inheritance â€” this
// type stands alone with only the catalog FK (PermissionId) + the catalog attribute the service sets (PermissionKey).
// (Legacy also declared a dead private "_permissionKey" field with no property; that remains dropped.)
public class TabPermission
{
    public int TabPermissionId { get; set; }

    // MIGRATION (QA-4 #4): FK to the [Permission] catalog â€” the legacy [PermissionID] column on [TabPermission].
    // Mapped to "PermissionID" by TabPermissionConfiguration. No Permission navigation is modeled.
    public int PermissionId { get; set; }

    // MIGRATION (QA-4 #4): legacy PermissionInfo.PermissionKey, set by TabService when constructing a grant.
    // Catalog attribute (physically on [Permission]), NOT a [TabPermission] column -> Ignore()d in the config;
    // the CLR property is retained for the service/DTO flow.
    public string? PermissionKey { get; set; }

    // MIGRATION: Legacy TabID initialized to Null.NullInteger (-1) -> nullable int. Scopes the permission to a tab (page).
    public int? TabId { get; set; }

    // MIGRATION: Legacy RoleID default Integer.Parse(glbRoleNothing) = -1 ("no role"/user-level grant). Sentinel preserved.
    public int RoleId { get; set; } = -1;

    public string? RoleName { get; set; }

    public bool AllowAccess { get; set; }

    // MIGRATION: Legacy UserID initialized to Null.NullInteger (-1) -> nullable int.
    public int? UserId { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }
}
