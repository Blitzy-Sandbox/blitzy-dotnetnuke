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
        builder.Property(fp => fp.PortalId).HasColumnName("PortalID"); // MIGRATION: preserves PortalId tenant scoping on folder permissions.
        builder.Property(fp => fp.RoleId).HasColumnName("RoleID");
        builder.Property(fp => fp.UserId).HasColumnName("UserID");

        // NOTE: FolderPermission has no inverse navigation in scope (no Folder entity in the 10-POCO model),
        // so no relationship is configured. Scalar columns FolderPath/RoleName/AllowAccess/Username/DisplayName
        // rely on convention (case-insensitive SQL Server collation).
    }
}
