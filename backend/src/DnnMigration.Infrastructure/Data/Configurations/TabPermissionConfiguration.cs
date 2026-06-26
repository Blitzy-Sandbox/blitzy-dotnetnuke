using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for TabPermission (legacy TabPermissionInfo,
// Library/Components/Security/Permissions/TabPermission.vb). In the legacy DB, [TabPermission] is a physically
// separate table with its OWN identity PK [TabPermissionID] and a [PermissionID] FK to the [Permission] catalog
// (6 columns: TabPermissionID, TabID, PermissionID, RoleID, AllowAccess, UserID).
// MIGRATION (QA-4 #4): the model now uses COMPOSITION (not inheritance + TPC), so this config owns the FULL
// mapping for the type: its own identity PK, the PermissionID FK column, and the remaining physical columns.
public sealed class TabPermissionConfiguration : IEntityTypeConfiguration<TabPermission>
{
    public void Configure(EntityTypeBuilder<TabPermission> builder)
    {
        // Legacy table [TabPermission] (02.02.00.SqlDataProvider).
        builder.ToTable("TabPermission");

        // MIGRATION (QA-4 #4): own identity PK = legacy [TabPermissionID] (was wrongly forced to [PermissionID] by TPC).
        builder.HasKey(tp => tp.TabPermissionId);

        // This type's OWN columns -> uppercase-ID legacy names. PermissionId is the FK to [Permission].
        builder.Property(tp => tp.TabPermissionId).HasColumnName("TabPermissionID");
        builder.Property(tp => tp.TabId).HasColumnName("TabID");
        builder.Property(tp => tp.PermissionId).HasColumnName("PermissionID");
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

        // MIGRATION (QA-4 #4): PermissionKey is a [Permission] CATALOG attribute consumed by TabService, NOT a
        // physical [TabPermission] column. Ignore the EF column mapping; the CLR property is retained for the
        // service/DTO flow (the catalog value is resolved from [Permission] via the PermissionID FK).
        builder.Ignore(tp => tp.PermissionKey);
    }
}
