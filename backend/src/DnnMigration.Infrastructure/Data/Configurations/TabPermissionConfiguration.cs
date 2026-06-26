using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for TabPermission (legacy TabPermissionInfo : PermissionInfo,
// Library/Components/Security/Permissions/TabPermission.vb). In the legacy DB, TabPermission is a physically
// separate table with its OWN identity PK [TabPermissionID] and a [PermissionID] FK to the Permission catalog.
// The C# model uses inheritance (TabPermission : Permission); reconciled by the TPC strategy on the base
// PermissionConfiguration. Here we map only this type's own table and own columns; the inherited key
// PermissionId (-> "PermissionID") is configured by the base config.
public sealed class TabPermissionConfiguration : IEntityTypeConfiguration<TabPermission>
{
    public void Configure(EntityTypeBuilder<TabPermission> builder)
    {
        // Legacy table [TabPermission] (02.02.00.SqlDataProvider). Do NOT call UseTpcMappingStrategy (base owns it).
        builder.ToTable("TabPermission");

        // This type's OWN columns -> uppercase-ID legacy names. (Inherited Permission* members configured by base.)
        builder.Property(tp => tp.TabPermissionId).HasColumnName("TabPermissionID");
        builder.Property(tp => tp.TabId).HasColumnName("TabID");
        builder.Property(tp => tp.RoleId).HasColumnName("RoleID");
        builder.Property(tp => tp.UserId).HasColumnName("UserID");

        // NOTE: the inverse relationship (Tab 1..N TabPermission) is owned by TabConfiguration via
        // HasMany(t => t.TabPermissions).WithOne().HasForeignKey(tp => tp.TabId). Do NOT reconfigure it here.

        // MIGRATION (CP2 review — TabPermissionConfiguration #1): RoleName, Username and DisplayName are NOT
        // physical [TabPermission] columns — they are view/computed values (vw_TabPermissions joins Roles/Users
        // and CASEs the special RoleIDs). Ignore them so EF convention does not emit SQL for columns that do not
        // exist on [TabPermission]. AllowAccess IS a physical column (left to convention); UserId (added by
        // 04.05.00) is mapped above.
        builder.Ignore(tp => tp.RoleName);
        builder.Ignore(tp => tp.Username);
        builder.Ignore(tp => tp.DisplayName);
    }
}
