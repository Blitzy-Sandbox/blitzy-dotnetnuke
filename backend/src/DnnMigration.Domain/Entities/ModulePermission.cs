namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.ModulePermissionInfo
// (Library/Components/Security/Permissions/ModulePermission.vb). Renamed to ModulePermission;
// preserves inheritance from the base Permission. XML attributes removed; persistence-ignorant POCO.
// Legacy default/copy constructors dropped (defaults now via initializers / EF hydration).
public class ModulePermission : Permission
{
    public int ModulePermissionId { get; set; }

    // MIGRATION: Legacy ModuleID initialized to Null.NullInteger (-1) -> nullable int. Scopes the permission to a module.
    public int? ModuleId { get; set; }

    // MIGRATION: Legacy RoleID default Integer.Parse(glbRoleNothing) = -1 ("no role"/user-level grant).
    // Sentinel preserved because downstream access checks compare against -1.
    public int RoleId { get; set; } = -1;

    public string? RoleName { get; set; }

    public bool AllowAccess { get; set; }

    // MIGRATION: Legacy UserID initialized to Null.NullInteger (-1) -> nullable int.
    public int? UserId { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }

    // MIGRATION: Preserved from legacy ModulePermissionInfo.Equals — de-duplication logic that prevented
    // duplicate entries in the legacy ModulePermissionCollection. Compares AllowAccess, ModuleId, RoleId, PermissionId.
    // Legacy used non-short-circuit And/Or; the && port is behaviorally equivalent (no side effects).
    public override bool Equals(object? obj)
    {
        if (obj is null || GetType() != obj.GetType())
        {
            return false;
        }

        var perm = (ModulePermission)obj;
        return AllowAccess == perm.AllowAccess
            && ModuleId == perm.ModuleId
            && RoleId == perm.RoleId
            && PermissionId == perm.PermissionId;
    }

    // MIGRATION: Added to satisfy the C# Equals/GetHashCode contract (CS0659) when overriding Equals.
    // Not present in legacy VB (which did not override GetHashCode); required for clean --warnaserror build.
    public override int GetHashCode() => HashCode.Combine(AllowAccess, ModuleId, RoleId, PermissionId);
}
