using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for FolderPermission (legacy FolderPermissionInfo : PermissionInfo,
// Library/Components/Security/Permissions/FolderPermission.vb). In the legacy DB, FolderPermission is a
// physically separate table with its OWN identity PK [FolderPermissionID] and a [PermissionID] FK to the
// Permission catalog. The C# model uses inheritance (FolderPermission : Permission); reconciled by the TPC
// strategy on the base PermissionConfiguration. Here we map only this type's own table and own columns;
// the inherited key PermissionId (-> "PermissionID") is configured by the base config.
public sealed class FolderPermissionConfiguration : IEntityTypeConfiguration<FolderPermission>
{
    public void Configure(EntityTypeBuilder<FolderPermission> builder)
    {
        // Legacy table [FolderPermission] (02.02.00.SqlDataProvider). Do NOT call UseTpcMappingStrategy (base owns it).
        builder.ToTable("FolderPermission");

        // This type's OWN columns -> uppercase-ID legacy names. (Inherited Permission* members configured by base.)
        builder.Property(fp => fp.FolderPermissionId).HasColumnName("FolderPermissionID");
        builder.Property(fp => fp.FolderId).HasColumnName("FolderID");
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
