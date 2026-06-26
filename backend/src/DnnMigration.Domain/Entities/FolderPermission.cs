namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.FolderPermissionInfo
// (Library/Components/Security/Permissions/FolderPermission.vb). Renamed to FolderPermission;
// persistence-ignorant POCO. XML attributes removed. A folder permission scopes a Permission to a folder + role/user.
//
// MIGRATION (QA-4 #4, CRITICAL): see ModulePermission for the full rationale. In the DNN SQL Server schema
// [FolderPermission] is a PHYSICALLY SEPARATE table with its OWN identity PK [FolderPermissionID] and a
// [PermissionID] FK to the [Permission] catalog (6 columns: FolderPermissionID, FolderID, PermissionID, RoleID,
// AllowAccess, UserID â€” there is NO PortalID column). The legacy "Inherits PermissionInfo" forced EF's TPC
// strategy to duplicate the base-Permission columns onto [FolderPermission] -> "Invalid column name" on real
// SQL Server. FIX: COMPOSITION (HAS-A), not inheritance â€” this type stands alone with only the catalog FK
// (PermissionId). PermissionKey is not modeled here (no FolderPermission code consumes it).
// (Legacy also declared a dead private "_permissionKey" field with no property; that remains dropped.)
public class FolderPermission
{
    public int FolderPermissionId { get; set; }

    // MIGRATION (QA-4 #4): FK to the [Permission] catalog â€” the legacy [PermissionID] column on [FolderPermission].
    // Mapped to "PermissionID" by FolderPermissionConfiguration. No Permission navigation is modeled.
    public int PermissionId { get; set; }

    // MIGRATION: Legacy FolderID initialized to Null.NullInteger (-1) -> nullable int.
    public int? FolderId { get; set; }

    // MIGRATION: Multi-tenant discriminator. Legacy PortalID initialized to Null.NullInteger (-1) -> nullable int.
    public int? PortalId { get; set; }

    public string? FolderPath { get; set; }

    // MIGRATION: Legacy RoleID default Integer.Parse(glbRoleNothing) = -1 ("no role"). Sentinel preserved.
    public int RoleId { get; set; } = -1;

    public string? RoleName { get; set; }

    public bool AllowAccess { get; set; }

    // MIGRATION: Legacy UserID initialized to Null.NullInteger (-1) -> nullable int.
    public int? UserId { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }
}
