using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for the standalone Permission CATALOG entity (legacy PermissionInfo,
// Library/Components/Security/Permissions/Permission.vb, namespace DotNetNuke.Security.Permissions).
//
// MIGRATION (QA-4 #4, CRITICAL): in the legacy DNN SQL Server schema the permission family is FOUR physically
// separate tables (Permission, ModulePermission, TabPermission, FolderPermission). [Permission] is the catalog
// table (5 columns: PermissionID, PermissionCode, ModuleDefID, PermissionKey, PermissionName); each of the other
// three has its OWN identity PK plus a [PermissionID] FK referencing [Permission]. The previous model mirrored
// the legacy VB "Inherits PermissionInfo" via EF Core's Table-Per-Concrete-type (TPC) strategy, which DUPLICATED
// these four catalog columns onto every child table, forced each child PK to [PermissionID], and emitted a
// [PermissionSequence] object -> "Invalid column name" against the real schema. The model now uses COMPOSITION
// (each child carries a PermissionID FK; see the child entities/configs), so TPC is removed and [Permission] is
// a plain standalone table. See MIGRATION_NOTES.md.
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        // MIGRATION (QA-4 #4): NO inheritance strategy â€” Permission is a standalone catalog table (the children
        // relate by composition via their own PermissionID FK column). UseTpcMappingStrategy() was REMOVED.

        // Legacy catalog table [Permission] (created in 02.02.00.SqlDataProvider).
        builder.ToTable("Permission");

        builder.HasKey(p => p.PermissionId);
        builder.Property(p => p.PermissionId).HasColumnName("PermissionID");

        // MIGRATION (QA-4 #2, MINOR): preserve the EXACT legacy column name [ModuleDefID]. EF convention would
        // emit [ModuleDefId] (resolves only under a case-INSENSITIVE collation) which diverges from the legacy
        // name and breaks under a case-sensitive collation.
        builder.Property(p => p.ModuleDefId).HasColumnName("ModuleDefID");

        // NOTE: the remaining catalog columns (PermissionCode, PermissionKey, PermissionName) match the legacy
        // names exactly under EF convention, so no explicit HasColumnName is required for them.
    }
}
