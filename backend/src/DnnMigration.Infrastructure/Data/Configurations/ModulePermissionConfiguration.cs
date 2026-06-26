using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for ModulePermission (legacy ModulePermissionInfo,
// Library/Components/Security/Permissions/ModulePermission.vb). In the legacy DB, [ModulePermission] is a
// physically separate table with its OWN identity PK [ModulePermissionID] and a [PermissionID] FK to the
// [Permission] catalog (6 columns: ModulePermissionID, ModuleID, PermissionID, RoleID, AllowAccess, UserID).
// MIGRATION (QA-4 #4): the model now uses COMPOSITION (not inheritance + TPC), so this config owns the FULL
// mapping for the type: its own identity PK, the PermissionID FK column, and the remaining physical columns.
public sealed class ModulePermissionConfiguration : IEntityTypeConfiguration<ModulePermission>
{
    public void Configure(EntityTypeBuilder<ModulePermission> builder)
    {
        // Legacy table [ModulePermission] (02.02.00.SqlDataProvider).
        builder.ToTable("ModulePermission");

        // MIGRATION (QA-4 #4): own identity PK = legacy [ModulePermissionID]. Under the prior TPC inheritance the
        // PK was wrongly forced to [PermissionID] (with a [PermissionSequence] default); composition restores it.
        builder.HasKey(mp => mp.ModulePermissionId);

        // This type's OWN columns mapped to their uppercase-ID legacy names. PermissionId is the FK to [Permission].
        builder.Property(mp => mp.ModulePermissionId).HasColumnName("ModulePermissionID");
        builder.Property(mp => mp.ModuleId).HasColumnName("ModuleID");
        builder.Property(mp => mp.PermissionId).HasColumnName("PermissionID");
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

        // MIGRATION (QA-4 #4): PermissionKey is a [Permission] CATALOG attribute consumed by ModuleService, NOT a
        // physical [ModulePermission] column. Ignore the EF column mapping; the CLR property is retained for the
        // service/DTO flow (the catalog value is resolved from [Permission] via the PermissionID FK).
        builder.Ignore(mp => mp.PermissionKey);
    }
}
