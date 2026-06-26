using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for FolderPermission (legacy FolderPermissionInfo,
// Library/Components/Security/Permissions/FolderPermission.vb). In the legacy DB, [FolderPermission] is a
// physically separate table with its OWN identity PK [FolderPermissionID] and a [PermissionID] FK to the
// [Permission] catalog (6 columns: FolderPermissionID, FolderID, PermissionID, RoleID, AllowAccess, UserID â€”
// there is NO PortalID column).
// MIGRATION (QA-4 #4): the model now uses COMPOSITION (not inheritance + TPC), so this config owns the FULL
// mapping for the type: its own identity PK, the PermissionID FK column, and the remaining physical columns.
public sealed class FolderPermissionConfiguration : IEntityTypeConfiguration<FolderPermission>
{
    public void Configure(EntityTypeBuilder<FolderPermission> builder)
    {
        // Legacy table [FolderPermission] (02.02.00.SqlDataProvider).
        builder.ToTable("FolderPermission");

        // MIGRATION (QA-4 #4): own identity PK = legacy [FolderPermissionID] (was wrongly forced to [PermissionID] by TPC).
        builder.HasKey(fp => fp.FolderPermissionId);

        // This type's OWN columns -> uppercase-ID legacy names. PermissionId is the FK to [Permission].
        builder.Property(fp => fp.FolderPermissionId).HasColumnName("FolderPermissionID");
        builder.Property(fp => fp.FolderId).HasColumnName("FolderID");
        builder.Property(fp => fp.PermissionId).HasColumnName("PermissionID");
        builder.Property(fp => fp.RoleId).HasColumnName("RoleID");
        builder.Property(fp => fp.UserId).HasColumnName("UserID");

        // MIGRATION (CP2 review — FolderPermissionConfiguration #1): the previously-mapped PortalId is NOT a column
        // of [FolderPermission]. Its CREATE TABLE is (FolderPermissionID, FolderID, PermissionID, RoleID,
        // AllowAccess), with UserID added by 04.05.00 — there is no PortalID column. The explicit
        // PortalId -> "PortalID" mapping was therefore removed. FolderPath/RoleName/Username/DisplayName are
        // likewise view/computed values, not physical columns. Ignore all of them so EF convention does not emit
        // SQL for non-existent columns. AllowAccess IS a physical column (left to convention); UserId is mapped
        // above. Folder-level tenant scoping is enforced via the [Folders] table / service layer, not a PortalID
        // column on [FolderPermission].
        builder.Ignore(fp => fp.PortalId);
        builder.Ignore(fp => fp.FolderPath);
        builder.Ignore(fp => fp.RoleName);
        builder.Ignore(fp => fp.Username);
        builder.Ignore(fp => fp.DisplayName);

        // NOTE: FolderPermission has no inverse navigation in scope (no Folder entity in the 10-POCO model),
        // so no relationship is configured.
    }
}
