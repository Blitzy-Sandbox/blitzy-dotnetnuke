using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for ModulePermission (legacy ModulePermissionInfo : PermissionInfo,
// Library/Components/Security/Permissions/ModulePermission.vb). In the legacy DB, ModulePermission is a
// physically separate table with its OWN identity PK [ModulePermissionID] and a [PermissionID] FK to the
// Permission catalog. The C# model uses inheritance (ModulePermission : Permission); this is reconciled by
// the TPC strategy declared on the base PermissionConfiguration. Here we map only this type's own table and
// own columns; the inherited key PermissionId (-> "PermissionID") is configured by the base config.
public sealed class ModulePermissionConfiguration : IEntityTypeConfiguration<ModulePermission>
{
    public void Configure(EntityTypeBuilder<ModulePermission> builder)
    {
        // Legacy table [ModulePermission] (02.02.00.SqlDataProvider). Do NOT call UseTpcMappingStrategy (base owns it).
        builder.ToTable("ModulePermission");

        // This type's OWN columns mapped to their uppercase-ID legacy names. (PermissionId/PermissionCode/
        // ModuleDefId/PermissionKey/PermissionName are inherited from Permission and configured by the base.)
        builder.Property(mp => mp.ModulePermissionId).HasColumnName("ModulePermissionID");
        builder.Property(mp => mp.ModuleId).HasColumnName("ModuleID");
        builder.Property(mp => mp.RoleId).HasColumnName("RoleID");
        builder.Property(mp => mp.UserId).HasColumnName("UserID");

        // NOTE: the inverse of this relationship (Module 1..N ModulePermission) is owned by ModuleConfiguration
        // via HasMany(m => m.ModulePermissions).WithOne().HasForeignKey(mp => mp.ModuleId). Do NOT reconfigure it here.

        // MIGRATION (CP2 review — ModulePermissionConfiguration #1): RoleName, Username and DisplayName are NOT
        // physical [ModulePermission] columns — they are view/computed values (vw_ModulePermissions joins
        // Roles/Users and CASEs the special RoleIDs). Ignore them so EF convention does not emit SQL for columns
        // that do not exist on [ModulePermission]. AllowAccess IS a physical column and is intentionally left to
        // convention; UserId (added by 04.05.00) is mapped above.
        builder.Ignore(mp => mp.RoleName);
        builder.Ignore(mp => mp.Username);
        builder.Ignore(mp => mp.DisplayName);
    }
}
