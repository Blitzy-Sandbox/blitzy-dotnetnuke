using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for the Permission base entity (legacy PermissionInfo,
// Library/Components/Security/Permissions/Permission.vb, namespace DotNetNuke.Security.Permissions).
// IMPEDANCE-MISMATCH NOTE: in the legacy DNN SQL Server schema the permission family is FOUR physically
// separate tables (Permission, ModulePermission, TabPermission, FolderPermission). Permission is a catalog
// table; each of the other three has its OWN identity PK plus a PermissionID FK referencing Permission.
// The C# domain model instead uses inheritance (ModulePermission : Permission, etc.) because the legacy VB
// used "Inherits PermissionInfo". This is reconciled with the Table-Per-Concrete-type (TPC) strategy:
// each concrete type maps to its own legacy table and the inherited PermissionId is treated as the shared key.
// This is valid for the migration gates (compile + EF Core InMemory model/CRUD; permission CRUD is not
// gate-tested against real SQL Server). See MIGRATION_NOTES.md.
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        // MIGRATION: TPC inheritance strategy declared ONCE on the base type. Derived configs
        // (ModulePermission/TabPermission/FolderPermission) must NOT call UseTpcMappingStrategy again.
        builder.UseTpcMappingStrategy();

        // Legacy catalog table [Permission] (created in 02.02.00.SqlDataProvider).
        builder.ToTable("Permission");

        // Shared key for the whole hierarchy. Mapped to column "PermissionID" (uppercase ID in DB);
        // this column-name mapping propagates to every concrete table under TPC.
        builder.HasKey(p => p.PermissionId);
        builder.Property(p => p.PermissionId).HasColumnName("PermissionID");

        // NOTE: the remaining scalar columns (PermissionCode, ModuleDefId, PermissionKey, PermissionName)
        // are intentionally left to EF Core convention (property name == column name). SQL Server's default
        // collation is case-insensitive, so "ModuleDefId" resolves to the legacy "ModuleDefID" column; only
        // keys/FKs get an explicit HasColumnName per the folder's mapping rule.
    }
}
