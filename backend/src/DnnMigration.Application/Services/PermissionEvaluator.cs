using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Security;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.Services;

// MIGRATION: Verbatim re-expression of the legacy PortalSecurity authorization helpers
// (Library/Components/Security/PortalSecurity.vb) and ModulePermissionController.HasModulePermission
// (Library/Components/Security/Permissions/ModulePermissionController.vb). The legacy logic is transcribed AS-IS
// per AAP 0.7.2 — no rule is optimized, reordered or "improved". The only structural change is that the ambient
// inputs the legacy Shared methods read (HttpContext.Current.Request.IsAuthenticated and
// UserController.GetCurrentUserInfo) are supplied explicitly through SecurityContext, making the evaluator a pure,
// deterministic, injectable function of its inputs. The service is stateless and therefore registered as a
// singleton.
public sealed class PermissionEvaluator : IPermissionEvaluator
{
    // MIGRATION: DNN special role names (Library/Components/Shared/Globals.vb):
    //   glbRoleAllUsersName  = "All Users"             (Globals.vb L100)
    //   glbRoleUnauthUserName = "Unauthenticated Users" (Globals.vb L102)
    private const string AllUsersRoleName = "All Users";
    private const string UnauthenticatedUsersRoleName = "Unauthenticated Users";

    // MIGRATION: PortalSecurity.IsInRole (PortalSecurity.vb L103-113), transcribed verbatim:
    //   If (role <> "" AndAlso Not role Is Nothing AndAlso
    //       ((context.Request.IsAuthenticated = False And role = glbRoleUnauthUserName))) Then
    //       Return True
    //   Else
    //       Return objUserInfo.IsInRole(role)
    //   End If
    public bool IsInRole(SecurityContext context, string? role)
    {
        if (!string.IsNullOrEmpty(role) && (context.IsAuthenticated == false && role == UnauthenticatedUsersRoleName))
        {
            return true;
        }

        return IsMemberOfRole(context, role);
    }

    // MIGRATION: PortalSecurity.IsInRoles (PortalSecurity.vb L115-136), transcribed verbatim:
    //   If Not roles Is Nothing Then
    //       For Each role In roles.Split(";"c)
    //           If objUserInfo.IsSuperUser Or (role <> "" AndAlso Not role Is Nothing AndAlso
    //              ((context.Request.IsAuthenticated = False And role = glbRoleUnauthUserName) Or
    //               role = glbRoleAllUsersName Or
    //               objUserInfo.IsInRole(role) = True)) Then
    //               Return True
    //           End If
    //       Next role
    //   End If
    //   Return False
    public bool IsInRoles(SecurityContext context, string? roles)
    {
        if (roles is not null)
        {
            foreach (string role in roles.Split(';'))
            {
                if (context.IsSuperUser
                    || (!string.IsNullOrEmpty(role)
                        && ((context.IsAuthenticated == false && role == UnauthenticatedUsersRoleName)
                            || role == AllUsersRoleName
                            || IsMemberOfRole(context, role))))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // MIGRATION: ModulePermissionController.HasModulePermission(ModulePermissionCollection, String), transcribed
    // verbatim:
    //   For Each objModulePermission In objModulePermissions
    //       If objModulePermission.PermissionKey = PermissionKey Then
    //           If Null.IsNull(objModulePermission.UserID) Then
    //               If PortalSecurity.IsInRoles(objModulePermission.RoleName) Then Return True
    //           Else
    //               If PortalSecurity.IsInRoles("[" & objModulePermission.UserID & "]") Then Return True
    //           End If
    //       End If
    //   Next
    // MIGRATION (documented, not fixed per AAP 0.7.2): this legacy overload does NOT consult AllowAccess, so a
    // matching key + role membership grants access even if an AllowAccess=false (deny) row exists. Preserved as-is.
    // MIGRATION: the legacy per-user grant evaluated IsInRoles("[" & UserID & "]"); in the BFF (no ambient
    // UserController) this is realized as a direct current-user comparison (context.UserId == permission.UserId),
    // which is the faithful intent of the bracketed-user role syntax.
    public bool HasModulePermission(SecurityContext context, IEnumerable<ModulePermission>? permissions, string permissionKey)
    {
        if (permissions is not null)
        {
            foreach (ModulePermission permission in permissions)
            {
                if (permission.PermissionKey == permissionKey)
                {
                    if (permission.UserId is null)
                    {
                        if (IsInRoles(context, permission.RoleName))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (context.UserId == permission.UserId)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // MIGRATION: TabPermissionController.HasTabPermission counterpart, mirroring the module-permission logic above
    // against a tab's permission grants (same key / role-list / per-user evaluation, AllowAccess not consulted).
    public bool HasTabPermission(SecurityContext context, IEnumerable<TabPermission>? permissions, string permissionKey)
    {
        if (permissions is not null)
        {
            foreach (TabPermission permission in permissions)
            {
                if (permission.PermissionKey == permissionKey)
                {
                    if (permission.UserId is null)
                    {
                        if (IsInRoles(context, permission.RoleName))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (context.UserId == permission.UserId)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // MIGRATION: PortalSecurity.HasNecessaryPermission(SecurityAccessLevel, PortalSettings, ModuleInfo, UserInfo)
    // (PortalSecurity.vb L517-549), transcribed verbatim. The Select Case only ever SETS blnAuthorized = True, so
    // the SuperUser short-circuit (set before the switch) is preserved for every access level — including Host,
    // whose case body is intentionally empty (Host access requires SuperUser).
    public bool HasNecessaryPermission(SecurityContext context, SecurityAccessLevel accessLevel, PermissionContext permission)
    {
        bool blnAuthorized = false;
        bool isAdmin = IsInRole(context, permission.AdministratorRoleName);
        bool isPageEditor = IsInRoles(context, permission.PageAdministratorRoles);
        bool canViewModule = IsInRoles(context, permission.AuthorizedViewRoles);
        bool canEditModule = HasModulePermission(context, permission.ModulePermissions, "EDIT");

        // MIGRATION: legacy guard was "If Not User Is Nothing AndAlso User.IsSuperUser". The SecurityContext is
        // never null here, so only the IsSuperUser flag is checked.
        if (context.IsSuperUser)
        {
            blnAuthorized = true;
        }

        switch (accessLevel)
        {
            case SecurityAccessLevel.Anonymous:
                blnAuthorized = true;
                break;
            case SecurityAccessLevel.View:
                if (isAdmin || isPageEditor || canViewModule)
                {
                    blnAuthorized = true;
                }
                break;
            case SecurityAccessLevel.Edit:
                if (isAdmin || isPageEditor)
                {
                    blnAuthorized = true;
                }
                else
                {
                    if (canViewModule && canEditModule)
                    {
                        blnAuthorized = true;
                    }
                }
                break;
            case SecurityAccessLevel.Admin:
                if (isAdmin || isPageEditor)
                {
                    blnAuthorized = true;
                }
                break;
            case SecurityAccessLevel.Host:
                // MIGRATION: legacy Host case body is empty — only a SuperUser (set above) is authorized.
                break;
        }

        return blnAuthorized;
    }

    // MIGRATION: stands in for the legacy objUserInfo.IsInRole(role) membership test. UserInfo.IsInRole checked
    // whether the role name was present in the user's Roles collection; here the same check is a case-sensitive
    // lookup against the role names carried on the SecurityContext (issued as ClaimTypes.Role claims). A null or
    // empty role is never a member (matching the legacy behavior where an empty role name matched nothing).
    private static bool IsMemberOfRole(SecurityContext context, string? role)
    {
        if (string.IsNullOrEmpty(role))
        {
            return false;
        }

        foreach (string memberRole in context.Roles)
        {
            if (memberRole == role)
            {
                return true;
            }
        }

        return false;
    }
}
