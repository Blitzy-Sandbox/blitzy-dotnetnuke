// -----------------------------------------------------------------------------------------------
//  PermissionConfiguration.cs
//
//  EF Core 8 Fluent API mappings for the DotNetNuke permission hierarchy. This single file declares
//  FOUR IEntityTypeConfiguration<T> implementations — one for the base Permission entity and one for
//  each of the three derived permission entities (ModulePermission, TabPermission, FolderPermission).
//  All four are auto-discovered and applied by DnnDbContext.OnModelCreating via
//  modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly); this file therefore
//  needs no manual registration.
//
//  MIGRATION (inheritance-vs-composition impedance mismatch — the reason this is the most complex
//  configuration in the solution):
//    * The Domain layer FAITHFULLY models the legacy VB.NET "*PermissionInfo : PermissionInfo"
//      inheritance (Library/Components/Security/Permissions/*.vb): ModulePermission, TabPermission,
//      and FolderPermission all derive from Permission in C#.
//    * The legacy RELATIONAL schema, however, is NORMALIZED / compositional
//      (Website/Providers/DataProviders/SqlDataProvider/02.02.00.SqlDataProvider, L659-L789): each
//      child table (ModulePermission L675, TabPermission L693, FolderPermission L659) has its OWN
//      IDENTITY primary key plus a PermissionID column that is a FOREIGN KEY to the singular
//      Permission table (L684). The child tables do NOT physically contain the base descriptive
//      columns (PermissionCode / ModuleDefID / PermissionKey / PermissionName); the legacy stored
//      procedures populated those (and the display fields RoleName / Username / DisplayName) by
//      JOINing to Permission / Roles / Users at read time.
//    * Therefore every derived type is OPTED OUT of EF Core's default table-per-hierarchy (TPH) via
//      builder.HasBaseType((Type?)null), turning each into a standalone root entity mapped to its own
//      real table, and the inherited descriptive properties plus the JOIN-populated convenience
//      properties are .Ignore()d because they are not physical columns of the child tables.
//
//  MIGRATION (single ownership of the permission relationships): this file is the SOLE owner of ALL
//  SIX named permission foreign keys. Each relationship is configured from exactly ONE side (here);
//  the principal-side configurations (ModuleConfiguration / TabConfiguration) must never re-declare
//  Module.ModulePermissions or Tab.TabPermissions — configuring a relationship from both sides would
//  make EF treat it as two distinct relationships.
//
//  DATA MODEL FIDELITY: table names, column names, and FK constraint names below are preserved
//  VERBATIM from the legacy schema; no table structure is altered. (Column name / FK name / table
//  name metadata is honored by the SQL Server provider in the API host; the EF Core InMemory provider
//  used by the integration tests — Validation Gate 5 — ignores ToTable/HasColumnName/HasConstraintName
//  but still validates keys, the TPH opt-out, ignored members, and the foreign-key wiring.)
// -----------------------------------------------------------------------------------------------

using System;                                          // MIGRATION: required for the (Type?)null cast used to opt each derived type out of TPH.
using Microsoft.EntityFrameworkCore;                   // IEntityTypeConfiguration<T>, DeleteBehavior, and the Fluent builder extension methods.
using Microsoft.EntityFrameworkCore.Metadata.Builders; // EntityTypeBuilder<T> — the parameter type of every Configure method below.
using DnnMigration.Domain.Entities;                    // Permission, ModulePermission, TabPermission, FolderPermission, Module, Tab.

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the base <see cref="Permission"/> entity. Maps it to the
/// existing (singular) <c>Permission</c> table and its five descriptive columns exactly as defined by
/// the legacy schema. This is the principal side of every <c>FK_*_Permission</c> relationship, all of
/// which are declared from the dependent side in the sibling configurations within this same file.
/// </summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <summary>
    /// Configures the <see cref="Permission"/> entity type: table, primary key, and the descriptive
    /// columns, each named exactly as in the legacy schema.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Permission"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        // MIGRATION: SINGULAR table name "Permission" (verified 02.02.00.SqlDataProvider L684). The
        // DnnDbContext DbSet is pluralized (Permissions) purely as a repository naming convenience; the
        // physical table is singular, so ToTable pins the real name explicitly.
        builder.ToTable("Permission");

        // Primary key: PermissionID (IDENTITY in the legacy schema, L685).
        builder.HasKey(e => e.PermissionID);
        builder.Property(e => e.PermissionID)
               .HasColumnName("PermissionID")
               .ValueGeneratedOnAdd();

        // Descriptive columns mapped 1:1 to their legacy column names (L686-L689).
        builder.Property(e => e.PermissionCode).HasColumnName("PermissionCode");
        builder.Property(e => e.ModuleDefID).HasColumnName("ModuleDefID");
        builder.Property(e => e.PermissionKey).HasColumnName("PermissionKey");
        builder.Property(e => e.PermissionName).HasColumnName("PermissionName");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="ModulePermission"/> entity. Maps it to the
/// existing <c>ModulePermission</c> table as a standalone root entity (opted out of the
/// <see cref="Permission"/> CLR inheritance hierarchy) and owns the two <c>ModulePermission</c> foreign
/// keys: <c>FK_ModulePermission_Modules</c> and <c>FK_ModulePermission_Permission</c>.
/// </summary>
public class ModulePermissionConfiguration : IEntityTypeConfiguration<ModulePermission>
{
    /// <summary>
    /// Configures the <see cref="ModulePermission"/> entity type: TPH opt-out, table, key, physical
    /// columns, ignored inherited/denormalized members, and its two owned foreign keys.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="ModulePermission"/> entity type.</param>
    public void Configure(EntityTypeBuilder<ModulePermission> builder)
    {
        // MIGRATION: domain uses inheritance; relational schema is composition — each *Permission is its
        // own table with its own PK plus a PermissionID FK to Permission. Opt out of EF's default TPH so
        // this derived CLR type maps to its own real table instead of sharing Permission's table behind a
        // discriminator column that does not exist in the legacy schema.
        builder.HasBaseType((Type?)null);

        // Table + IDENTITY primary key (verified 02.02.00.SqlDataProvider L675-L676; PK_ModulePermission L717).
        builder.ToTable("ModulePermission");
        builder.HasKey(e => e.ModulePermissionID);
        builder.Property(e => e.ModulePermissionID)
               .HasColumnName("ModulePermissionID")
               .ValueGeneratedOnAdd();

        // Physical columns of the ModulePermission table (L677-L680). PermissionID is inherited from
        // Permission but IS a real physical column here (the FK to Permission), so it is mapped, not
        // ignored — it doubles as the FK property configured further below.
        builder.Property(e => e.ModuleID).HasColumnName("ModuleID");
        builder.Property(e => e.PermissionID).HasColumnName("PermissionID");
        builder.Property(e => e.RoleID).HasColumnName("RoleID");
        builder.Property(e => e.AllowAccess).HasColumnName("AllowAccess");

        // MIGRATION: base descriptive fields were JOIN-populated from Permission; not physical
        // ModulePermission columns. Ignore them so EF does not expect columns that do not exist.
        builder.Ignore(e => e.PermissionCode);
        builder.Ignore(e => e.ModuleDefID);
        builder.Ignore(e => e.PermissionKey);
        builder.Ignore(e => e.PermissionName);

        // MIGRATION: denormalized display fields, not persisted here (JOIN-populated from Roles/Users at
        // read time in the legacy stored procedures). Ignore them.
        builder.Ignore(e => e.RoleName);
        builder.Ignore(e => e.UserID);
        builder.Ignore(e => e.Username);
        builder.Ignore(e => e.DisplayName);

        // FK_ModulePermission_Modules: ModuleID -> Modules.ModuleID. Configured from THIS (dependent)
        // side; the inverse collection is Module.ModulePermissions. This file is the sole owner of the
        // relationship, so ModuleConfiguration must NOT re-declare Module.ModulePermissions.
        builder.HasOne<Module>()
               .WithMany(m => m.ModulePermissions)
               .HasForeignKey(mp => mp.ModuleID)
               .HasConstraintName("FK_ModulePermission_Modules")
               .OnDelete(DeleteBehavior.Cascade);

        // FK_ModulePermission_Permission: PermissionID -> Permission.PermissionID. Permission exposes no
        // inverse collection for module permissions, so WithMany() is left without a navigation.
        builder.HasOne<Permission>()
               .WithMany()
               .HasForeignKey(mp => mp.PermissionID)
               .HasConstraintName("FK_ModulePermission_Permission")
               .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: multiple cascade paths — ModulePermission has TWO Cascade FKs (to Modules and to
        // Permission). SQL Server rejects multiple cascade paths at DB-creation time, but the legacy
        // (authoritative) database already defines BOTH as ON DELETE CASCADE, no EF migration is generated
        // against that existing schema, and Gate 5 uses the EF Core InMemory provider (which has no such
        // restriction). Cascade is retained on both per Data Model Fidelity.

        // MIGRATION: RoleID is a plain int column, NOT a modeled FK. The legacy schema defines no
        // FK_ModulePermission_Roles, and RoleID carries the sentinel value -1 to mean "all roles"; a real
        // FK constraint would reject that sentinel. It is intentionally left as a plain column.
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="TabPermission"/> entity. Maps it to the existing
/// <c>TabPermission</c> table as a standalone root entity (opted out of the <see cref="Permission"/> CLR
/// inheritance hierarchy) and owns the two <c>TabPermission</c> foreign keys:
/// <c>FK_TabPermission_Tabs</c> and <c>FK_TabPermission_Permission</c>.
/// </summary>
public class TabPermissionConfiguration : IEntityTypeConfiguration<TabPermission>
{
    /// <summary>
    /// Configures the <see cref="TabPermission"/> entity type: TPH opt-out, table, key, physical columns,
    /// ignored inherited/denormalized members, and its two owned foreign keys.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="TabPermission"/> entity type.</param>
    public void Configure(EntityTypeBuilder<TabPermission> builder)
    {
        // MIGRATION: domain uses inheritance; relational schema is composition — opt this derived type out
        // of EF's default TPH so it maps to its own real TabPermission table (own PK + PermissionID FK)
        // rather than sharing Permission's table behind a nonexistent discriminator column.
        builder.HasBaseType((Type?)null);

        // Table + IDENTITY primary key (verified 02.02.00.SqlDataProvider L693-L694; PK_TabPermission L731).
        builder.ToTable("TabPermission");
        builder.HasKey(e => e.TabPermissionID);
        builder.Property(e => e.TabPermissionID)
               .HasColumnName("TabPermissionID")
               .ValueGeneratedOnAdd();

        // Physical columns of the TabPermission table (L695-L698). PermissionID is inherited but is a real
        // physical FK column here.
        builder.Property(e => e.TabID).HasColumnName("TabID");
        builder.Property(e => e.PermissionID).HasColumnName("PermissionID");
        builder.Property(e => e.RoleID).HasColumnName("RoleID");
        builder.Property(e => e.AllowAccess).HasColumnName("AllowAccess");

        // MIGRATION: base descriptive fields were JOIN-populated from Permission; not physical
        // TabPermission columns. Ignore them.
        builder.Ignore(e => e.PermissionCode);
        builder.Ignore(e => e.ModuleDefID);
        builder.Ignore(e => e.PermissionKey);
        builder.Ignore(e => e.PermissionName);

        // MIGRATION: denormalized display fields, not persisted here. Ignore them.
        builder.Ignore(e => e.RoleName);
        builder.Ignore(e => e.UserID);
        builder.Ignore(e => e.Username);
        builder.Ignore(e => e.DisplayName);

        // FK_TabPermission_Tabs: TabID -> Tabs.TabID. Configured from THIS (dependent) side; the inverse
        // collection is Tab.TabPermissions. This file is the sole owner — TabConfiguration must NOT
        // re-declare Tab.TabPermissions.
        builder.HasOne<Tab>()
               .WithMany(t => t.TabPermissions)
               .HasForeignKey(tp => tp.TabID)
               .HasConstraintName("FK_TabPermission_Tabs")
               .OnDelete(DeleteBehavior.Cascade);

        // FK_TabPermission_Permission: PermissionID -> Permission.PermissionID. No inverse navigation on
        // Permission, so WithMany() is left empty.
        builder.HasOne<Permission>()
               .WithMany()
               .HasForeignKey(tp => tp.PermissionID)
               .HasConstraintName("FK_TabPermission_Permission")
               .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: multiple cascade paths — TabPermission has TWO Cascade FKs (to Tabs and to
        // Permission). Retained per Data Model Fidelity for the same reasons noted on ModulePermission
        // (legacy DB authoritative; no migration generated; Gate 5 InMemory has no cascade-path restriction).

        // MIGRATION: RoleID is a plain int column, NOT a modeled FK (no FK_TabPermission_Roles in the
        // schema; RoleID = -1 means "all roles"). Intentionally left as a plain column.
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="FolderPermission"/> entity. Maps it to the
/// existing <c>FolderPermission</c> table as a standalone root entity (opted out of the
/// <see cref="Permission"/> CLR inheritance hierarchy) and owns the <c>FK_FolderPermission_Permission</c>
/// foreign key. The legacy <c>FK_FolderPermission_Folders</c> is deliberately NOT modeled because the
/// <c>Folders</c> entity is out of migration scope.
/// </summary>
public class FolderPermissionConfiguration : IEntityTypeConfiguration<FolderPermission>
{
    /// <summary>
    /// Configures the <see cref="FolderPermission"/> entity type: TPH opt-out, table, key, physical
    /// columns, ignored inherited/denormalized members, and its single modeled foreign key
    /// (<c>FK_FolderPermission_Permission</c>).
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="FolderPermission"/> entity type.</param>
    public void Configure(EntityTypeBuilder<FolderPermission> builder)
    {
        // MIGRATION: domain uses inheritance; relational schema is composition — opt this derived type out
        // of EF's default TPH so it maps to its own real FolderPermission table (own PK + PermissionID FK)
        // rather than sharing Permission's table behind a nonexistent discriminator column.
        builder.HasBaseType((Type?)null);

        // Table + IDENTITY primary key (verified 02.02.00.SqlDataProvider L659-L660; PK_FolderPermission L703).
        builder.ToTable("FolderPermission");
        builder.HasKey(e => e.FolderPermissionID);
        builder.Property(e => e.FolderPermissionID)
               .HasColumnName("FolderPermissionID")
               .ValueGeneratedOnAdd();

        // Physical columns of the FolderPermission table (L661-L664). PermissionID is inherited but is a
        // real physical FK column here.
        builder.Property(e => e.FolderID).HasColumnName("FolderID");
        builder.Property(e => e.PermissionID).HasColumnName("PermissionID");
        builder.Property(e => e.RoleID).HasColumnName("RoleID");
        builder.Property(e => e.AllowAccess).HasColumnName("AllowAccess");

        // MIGRATION: base descriptive fields were JOIN-populated from Permission; not physical
        // FolderPermission columns. Ignore them.
        builder.Ignore(e => e.PermissionCode);
        builder.Ignore(e => e.ModuleDefID);
        builder.Ignore(e => e.PermissionKey);
        builder.Ignore(e => e.PermissionName);

        // MIGRATION: PortalID/FolderPath are denormalized (JOIN to Folders), not FolderPermission columns.
        builder.Ignore(e => e.PortalID);
        builder.Ignore(e => e.FolderPath);

        // MIGRATION: denormalized display fields, not persisted here. Ignore them.
        builder.Ignore(e => e.RoleName);
        builder.Ignore(e => e.UserID);
        builder.Ignore(e => e.Username);
        builder.Ignore(e => e.DisplayName);

        // FK_FolderPermission_Permission: PermissionID -> Permission.PermissionID. No inverse navigation on
        // Permission, so WithMany() is left empty.
        builder.HasOne<Permission>()
               .WithMany()
               .HasForeignKey(fp => fp.PermissionID)
               .HasConstraintName("FK_FolderPermission_Permission")
               .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: Folders entity not in migration scope; FolderID retained as a plain int column and
        // FK_FolderPermission_Folders is NOT modeled. Modeling it would require introducing an
        // out-of-scope Folders entity/DbSet.

        // MIGRATION: RoleID is a plain int column, NOT a modeled FK (no FK_FolderPermission_Roles in the
        // schema; RoleID = -1 means "all roles"). Intentionally left as a plain column.
    }
}
