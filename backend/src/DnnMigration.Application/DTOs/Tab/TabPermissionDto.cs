namespace DnnMigration.Application.DTOs.Tab;

// MIGRATION: CP1 review (TabService #2, CRITICAL) — inbound tab-permission grant carried on
// CreateTabRequest/UpdateTabRequest so the Application layer can port the legacy tab-permission lifecycle
// (TabController.AddTab L336-349 add-with-AllowAccess-filter, UpdateTab L799-808 delete-all-then-re-add with the
// AllowAccess filter). Projected from the legacy DotNetNuke.Security.Permissions.TabPermissionInfo
// (Library/Components/Security/Permissions/TabPermission.vb). This is a request/transport shape — the Domain
// TabPermission entity is never accepted directly from the client. TabResponse intentionally OMITS the permission
// collection (review PASSED — keeping it omitted prevents an over-fetch and a cyclic AutoMapper graph), so this
// DTO is inbound-only.
//
// MIGRATION: Unlike the Module permission lifecycle, the legacy TAB add path ALSO applies the AllowAccess filter
// (AddTab L345: `If objTabPermission.AllowAccess Then AddTabPermission`), and the tab update diff has NO
// InheritViewPermissions / PermissionKey = "VIEW" special case (that skip is Module-only, UpdateModule L1106-1112).
// TabService applies the correct rule per operation.
//
// Mutable class (request DTO). No business logic or validation here (FluentValidation lives in
// Application/Validators; permission diff rules live in TabService).
public class TabPermissionDto
{
    // MIGRATION: Legacy TabPermissionInfo.RoleID. Default -1 (Integer.Parse(glbRoleNothing)) means "no role"
    // (a user-level grant). The -1 sentinel is preserved because downstream access checks compare against it.
    public int RoleId { get; set; } = -1;

    // MIGRATION: Legacy TabPermissionInfo.RoleName (display only; the authoritative key is RoleId).
    public string? RoleName { get; set; }

    // MIGRATION: Legacy TabPermissionInfo.UserID (Null.NullInteger -> nullable). Set for user-level grants.
    public int? UserId { get; set; }

    // MIGRATION: Legacy TabPermissionInfo.Username (display only).
    public string? Username { get; set; }

    // MIGRATION: Legacy TabPermissionInfo.DisplayName (display only).
    public string? DisplayName { get; set; }

    // MIGRATION: Legacy PermissionInfo.PermissionID — the permission being granted/denied.
    public int PermissionId { get; set; }

    // MIGRATION: Legacy PermissionInfo.PermissionKey (e.g. "VIEW", "EDIT"). Carried for parity with the
    // permission shape; the tab update diff does NOT special-case "VIEW" (that is a Module-only rule).
    public string? PermissionKey { get; set; }

    // MIGRATION: Legacy TabPermissionInfo.AllowAccess. On BOTH add (AddTab L345) and update (UpdateTab L804) the
    // legacy code persisted ONLY AllowAccess grants. TabService applies this filter in both paths.
    public bool AllowAccess { get; set; }
}
