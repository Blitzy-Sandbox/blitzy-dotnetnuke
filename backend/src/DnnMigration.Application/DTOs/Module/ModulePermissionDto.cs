namespace DnnMigration.Application.DTOs.Module;

// MIGRATION: CP1 review (ModuleService #3, CRITICAL) — inbound module-permission grant carried on
// CreateModuleRequest/UpdateModuleRequest so the Application layer can port the legacy module-permission
// lifecycle (ModuleController.AddModule L649-659 add-all, UpdateModule L1099-1118 delete-all-then-re-add with
// the InheritViewPermissions && PermissionKey == "VIEW" skip and the AllowAccess filter). Projected from the
// legacy DotNetNuke.Security.Permissions.ModulePermissionInfo (Library/Components/Security/Permissions/
// ModulePermission.vb). This is a request/transport shape — the Domain ModulePermission entity is never accepted
// directly from the client. ModuleResponse intentionally OMITS the permission collection (review PASSED — keeping
// it omitted prevents an over-fetch and a cyclic AutoMapper graph), so this DTO is inbound-only.
//
// Mutable class (request DTO). No business logic or validation here (FluentValidation lives in
// Application/Validators; permission grouping/diff rules live in ModuleService).
public class ModulePermissionDto
{
    // MIGRATION: Legacy ModulePermissionInfo.RoleID. Default -1 (Integer.Parse(glbRoleNothing)) means "no role"
    // (a user-level grant). The -1 sentinel is preserved because downstream access checks compare against it.
    public int RoleId { get; set; } = -1;

    // MIGRATION: Legacy ModulePermissionInfo.RoleName (display only; the authoritative key is RoleId).
    public string? RoleName { get; set; }

    // MIGRATION: Legacy ModulePermissionInfo.UserID (Null.NullInteger -> nullable). Set for user-level grants.
    public int? UserId { get; set; }

    // MIGRATION: Legacy ModulePermissionInfo.DisplayName (display only).
    public string? DisplayName { get; set; }

    // MIGRATION: Legacy PermissionInfo.PermissionID — the permission being granted/denied.
    public int PermissionId { get; set; }

    // MIGRATION: Legacy PermissionInfo.PermissionKey (e.g. "VIEW", "EDIT"). Drives the legacy
    // InheritViewPermissions && PermissionKey = "VIEW" special case in UpdateModule (L1106-1112).
    public string? PermissionKey { get; set; }

    // MIGRATION: Legacy ModulePermissionInfo.AllowAccess. On UPDATE the legacy code persisted only AllowAccess
    // grants (L1106-1118); on ADD it persisted every supplied permission (L649-659). ModuleService applies the
    // correct rule per operation.
    public bool AllowAccess { get; set; }
}
