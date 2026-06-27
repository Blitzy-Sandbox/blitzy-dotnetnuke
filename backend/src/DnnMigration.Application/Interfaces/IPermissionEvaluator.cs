using DnnMigration.Application.Security;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.Interfaces;

// MIGRATION: Reusable authorization service that re-expresses the static legacy PortalSecurity helpers
// (Library/Components/Security/PortalSecurity.vb) and ModulePermissionController.HasModulePermission
// (Library/Components/Security/Permissions/ModulePermissionController.vb) as an injectable, testable contract.
// The legacy helpers were Shared (static) methods that reached into ambient HttpContext + UserController state;
// here every input is supplied explicitly via SecurityContext / PermissionContext so the same decisions can be
// made in the stateless BFF and covered by unit tests. The migrated logic is transcribed VERBATIM (AAP 0.7.2) —
// it is not optimized or "improved".
public interface IPermissionEvaluator
{
    /// <summary>
    /// MIGRATION: PortalSecurity.IsInRole (PortalSecurity.vb L103-113). Returns true when the (non-empty) role is
    /// the "Unauthenticated Users" special role and the principal is unauthenticated; otherwise returns whether
    /// the principal is a direct member of the role.
    /// </summary>
    bool IsInRole(SecurityContext context, string? role);

    /// <summary>
    /// MIGRATION: PortalSecurity.IsInRoles (PortalSecurity.vb L115-136). Evaluates a DNN ";"-delimited permission
    /// string: returns true if the principal is a SuperUser, or if any role in the list matches the special
    /// "Unauthenticated Users"/"All Users" rules or direct membership.
    /// </summary>
    bool IsInRoles(SecurityContext context, string? roles);

    /// <summary>
    /// MIGRATION: ModulePermissionController.HasModulePermission(ModulePermissionCollection, String). Returns true
    /// if any permission with the given key grants access to the principal (by role list, or by direct per-user
    /// grant). Preserves the legacy behavior of NOT consulting AllowAccess.
    /// </summary>
    bool HasModulePermission(SecurityContext context, IEnumerable<ModulePermission>? permissions, string permissionKey);

    /// <summary>
    /// Tab-permission counterpart of <see cref="HasModulePermission"/>, mirroring the same legacy permission-key /
    /// role-list / per-user evaluation against a tab's permission grants (TabPermissionController.HasTabPermission).
    /// </summary>
    bool HasTabPermission(SecurityContext context, IEnumerable<TabPermission>? permissions, string permissionKey);

    /// <summary>
    /// MIGRATION: PortalSecurity.HasNecessaryPermission(SecurityAccessLevel, PortalSettings, ModuleInfo, UserInfo)
    /// (PortalSecurity.vb L517-549). Composes the admin / page-editor / view / edit checks for the requested
    /// <paramref name="accessLevel"/>, with SuperUsers authorized for every level.
    /// </summary>
    bool HasNecessaryPermission(SecurityContext context, SecurityAccessLevel accessLevel, PermissionContext permission);
}
