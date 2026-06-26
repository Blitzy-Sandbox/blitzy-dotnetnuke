namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.ModulePermissionInfo
// (Library/Components/Security/Permissions/ModulePermission.vb). Renamed to ModulePermission;
// persistence-ignorant POCO. XML attributes removed. Legacy default/copy constructors dropped.
//
// MIGRATION (QA-4 #4, CRITICAL): the legacy VB used "Inherits PermissionInfo", but in the DNN SQL Server schema
// [ModulePermission] is a PHYSICALLY SEPARATE table with its OWN identity PK [ModulePermissionID] and a
// [PermissionID] FK to the [Permission] catalog (6 columns total: ModulePermissionID, ModuleID, PermissionID,
// RoleID, AllowAccess, UserID). Modeling it as C# inheritance forced EF Core's TPC strategy to DUPLICATE the four
// base-Permission columns (PermissionCode/ModuleDefId/PermissionKey/PermissionName) onto [ModulePermission] and to
// change the PK to [PermissionID] via a [PermissionSequence] object -> "Invalid column name" on real SQL Server.
// FIX: COMPOSITION (HAS-A), not inheritance. This type now stands alone and carries only the catalog FK
// (PermissionId) plus the one catalog attribute the service layer actually sets (PermissionKey). The unused base
// attributes PermissionCode/ModuleDefId/PermissionName are dropped (never referenced on a child instance).
public class ModulePermission
{
    public int ModulePermissionId { get; set; }

    // MIGRATION (QA-4 #4): FK to the [Permission] catalog â€” the legacy [PermissionID] column on [ModulePermission].
    // Mapped to "PermissionID" by ModulePermissionConfiguration. No Permission navigation is modeled because no
    // child code reads catalog attributes (PermissionCode/ModuleDefId/PermissionName) off a child instance.
    public int PermissionId { get; set; }

    // MIGRATION (QA-4 #4): legacy PermissionInfo.PermissionKey, set by ModuleService when constructing a grant.
    // It is a CATALOG attribute (physically on [Permission]), NOT a [ModulePermission] column, so it is Ignore()d
    // in ModulePermissionConfiguration; the CLR property is retained for the service/DTO flow.
    public string? PermissionKey { get; set; }

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
