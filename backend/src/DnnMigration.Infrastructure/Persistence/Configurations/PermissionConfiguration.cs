// =============================================================================
// PermissionConfiguration.cs
// -----------------------------------------------------------------------------
// EF Core 8 Fluent API mapping for the DotNetNuke security "permission aggregate":
// the Permission base entity plus its three junction entities
// (ModulePermission, TabPermission, FolderPermission), onto the EXISTING
// (unchanged) DotNetNuke 4.9.0.85 database schema.
//
// MIGRATION (structural deviation — the hardest mapping in this migration):
// The Domain models the three junctions as C# SUBCLASSES of Permission
// (ModulePermission : Permission, TabPermission : Permission,
// FolderPermission : Permission), mirroring the legacy VB "Inherits PermissionInfo"
// relationship. The DNN schema, however, stores them in FOUR SEPARATE TABLES,
// each with its OWN identity primary key (ModulePermissionID / TabPermissionID /
// FolderPermissionID) PLUS a PermissionID FOREIGN-KEY column back to the
// Permission table. This is therefore NEITHER Table-Per-Hierarchy (TPH) NOR
// Table-Per-Type (TPT): EF's default inheritance convention would (incorrectly)
// fold all four CLR types into a single "Permission" table with a discriminator.
// To reproduce the real schema, the EF inheritance hierarchy is BROKEN in every
// derived Configure method via `builder.HasBaseType((Type?)null)`, promoting each
// derived type to an INDEPENDENT root entity with its own table and primary key.
// The four base-only scalar properties inherited from Permission (PermissionCode,
// ModuleDefID, PermissionKey, PermissionName) have no column on the junction
// tables and are therefore Ignored on each derived type, while the inherited
// PermissionID property IS mapped as the real FK column.
//
// ADR-002 (schema preservation): the schema is mapped UNCHANGED — no EF
// migrations, no schema generation, no SQL-Server-only defaults, no data
// migration. Table and column names are reproduced verbatim from the baseline
// install DDL (Website/Providers/DataProviders/SqlDataProvider/
// DotNetNuke.Schema.SqlDataProvider) and the SqlDataProvider.vb Add/Update
// stored-procedure call sites. NOTE: all four physical table names are SINGULAR
// (Permission / ModulePermission / TabPermission / FolderPermission) even though
// the DnnDbContext DbSet accessors are pluralized.
//
// InMemory-provider safety (Gate 5): only provider-agnostic primitives are used
// (HasBaseType / ToTable / HasKey / Property / Ignore). No HasDefaultValueSql,
// HasComputedColumnSql, raw SQL, or ValueGeneratedNever. Integer identity keys
// are left at the EF convention default (ValueGeneratedOnAdd) so the InMemory
// provider can auto-assign keys. The entire EF model is built ONCE for the
// integration-test fixtures; an incorrect mapping here fails the whole model
// build, so this file's correctness gates ALL Gate 5 integration tests.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core <see cref="IEntityTypeConfiguration{TEntity}"/> that maps the entire
/// DotNetNuke permission aggregate — the <see cref="Permission"/> base entity and
/// the <see cref="ModulePermission"/>, <see cref="TabPermission"/> and
/// <see cref="FolderPermission"/> junction entities — onto their pre-existing
/// DotNetNuke 4.9.0.85 tables. A single configuration class hosts all four
/// <c>Configure</c> overloads because they form one tightly-coupled persistence
/// concern and share the inheritance-breaking strategy described below.
/// </summary>
/// <remarks>
/// <para>
/// Discovered automatically by <c>DnnDbContext.OnModelCreating</c> via
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>,
/// which detects a single public type implementing the interface for more than one
/// entity, as this one does — so it requires no explicit registration.
/// </para>
/// <para>
/// The Domain entities model the three junctions as subclasses of
/// <see cref="Permission"/>, but the database uses four independent tables with
/// distinct identity primary keys and a <c>PermissionID</c> foreign-key column on
/// each junction (neither TPH nor TPT). Each derived <c>Configure</c> therefore
/// calls <c>builder.HasBaseType((Type?)null)</c> FIRST to detach the type from the
/// EF inheritance hierarchy and map it as a standalone root entity. The
/// <c>(Type?)</c> cast is required to disambiguate the <c>HasBaseType(Type?)</c> /
/// <c>HasBaseType(string?)</c> overloads (a bare <c>null</c> is a CS0121
/// ambiguity error).
/// </para>
/// <para>
/// Per ADR-002 the schema is mapped exactly as it exists in the database — there
/// are no EF migrations and no schema-generation side effects — and only
/// provider-agnostic relational metadata is configured, so the same model builds
/// cleanly under both the SQL Server provider (production) and the EF Core
/// InMemory provider (integration-test fixtures, Gate 5).
/// </para>
/// </remarks>
public sealed class PermissionConfiguration :
    IEntityTypeConfiguration<Permission>,
    IEntityTypeConfiguration<FolderPermission>,
    IEntityTypeConfiguration<ModulePermission>,
    IEntityTypeConfiguration<TabPermission>
{
    /// <summary>
    /// Configures the <see cref="Permission"/> base entity against the existing
    /// SINGULAR <c>dbo.Permission</c> table (primary key <c>PermissionID</c>).
    /// This is the only one of the four types configured as a normal root entity:
    /// it is the EF base of the CLR hierarchy and therefore does NOT call
    /// <c>HasBaseType</c>.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="Permission"/>.</param>
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        // Map onto the pre-existing physical table; schema "dbo", SINGULAR table
        // name reproduced verbatim from the DDL (ADR-002 — no schema generation).
        builder.ToTable("Permission", "dbo");

        // Primary key. PermissionID is an IDENTITY(1,1) column; leaving the int-PK
        // convention intact preserves ValueGeneratedOnAdd so the InMemory provider
        // can auto-assign keys on insert. (Do NOT call ValueGeneratedNever.)
        builder.HasKey(p => p.PermissionID);

        // The 5 physical Permission columns (DNN 4.9.0.85 schema, verbatim names).
        // Property names already equal their column names, so HasColumnName is
        // unnecessary; the explicit Property declarations make this configuration
        // the single, unambiguous source of truth for the column set.
        builder.Property(p => p.PermissionID);      // [PermissionID] int NOT NULL IDENTITY(1,1)
        builder.Property(p => p.PermissionCode);    // [PermissionCode] varchar(50) NOT NULL
        builder.Property(p => p.ModuleDefID);       // [ModuleDefID] int NOT NULL
        builder.Property(p => p.PermissionKey);     // [PermissionKey] varchar(20) NOT NULL
        builder.Property(p => p.PermissionName);    // [PermissionName] varchar(50) NOT NULL

        // MIGRATION: the base Permission has NO navigation properties to the
        // junction entities (the legacy PermissionInfo did not either), so NO EF
        // relationships are configured here. Each junction references Permission
        // only through a scalar PermissionID FK column mapped on the derived type.
    }

    /// <summary>
    /// Configures the <see cref="ModulePermission"/> junction against the existing
    /// SINGULAR <c>dbo.ModulePermission</c> table (primary key
    /// <c>ModulePermissionID</c>), detaching it from the <see cref="Permission"/>
    /// EF inheritance hierarchy so it maps to its own table.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="ModulePermission"/>.</param>
    public void Configure(EntityTypeBuilder<ModulePermission> builder)
    {
        // MIGRATION (inheritance break): ModulePermission is a CLR subclass of
        // Permission, but the DB stores it in its OWN table with its OWN identity
        // PK (ModulePermissionID) plus a PermissionID FK — not TPH/TPT. Detach it
        // from the EF inheritance hierarchy so it maps as an independent root
        // entity. The (Type?) cast is MANDATORY: a bare HasBaseType(null) is
        // ambiguous between the (string?) and (Type?) overloads (CS0121).
        builder.HasBaseType((Type?)null);

        // Map onto the pre-existing physical table; schema "dbo", SINGULAR name.
        builder.ToTable("ModulePermission", "dbo");

        // Own primary key (NOT the inherited PermissionID). ModulePermissionID is
        // IDENTITY(1,1); the convention default ValueGeneratedOnAdd is preserved.
        builder.HasKey(mp => mp.ModulePermissionID);

        // Real physical columns, verified from AddModulePermission /
        // UpdateModulePermission (SqlDataProvider.vb) and the CREATE TABLE DDL.
        builder.Property(mp => mp.ModulePermissionID);  // [ModulePermissionID] int NOT NULL IDENTITY(1,1)
        builder.Property(mp => mp.ModuleID);            // [ModuleID] int NOT NULL (scalar FK; relationship in ModuleConfiguration)

        // MIGRATION: PermissionID is INHERITED from the Permission base type but is
        // a REAL FK column on the ModulePermission table — it is MAPPED here (NOT
        // Ignored). It stays a plain scalar int; no EF relationship is configured.
        builder.Property(mp => mp.PermissionID);        // [PermissionID] int NOT NULL (FK -> Permission)

        // MIGRATION: in the final DNN 4.9 schema the DB RoleID is NULLABLE (the
        // proc passes GetRoleNull(roleID)) and UserID is NULLABLE (GetNull(UserID)),
        // using the legacy -1 / Null.NullInteger sentinel for "all roles/users".
        // The Domain entity keeps RoleID/UserID as non-nullable int per ADR-002
        // (entities are not changed), so they are mapped AS-IS. Do NOT add
        // .IsRequired(false) to a non-nullable int property.
        builder.Property(mp => mp.RoleID);              // [RoleID] int (nullable in DB; non-nullable in entity)
        builder.Property(mp => mp.AllowAccess);         // [AllowAccess] bit NOT NULL
        builder.Property(mp => mp.UserID);              // [UserID] int (nullable in DB; added by post-baseline upgrade)

        // MIGRATION: RoleName / Username / DisplayName are NOT physical
        // ModulePermission columns — legacy code populated them via JOINs to the
        // Roles / Users tables. They are carried as plain scalar properties (NOT
        // Ignored) so a ModulePermission round-trips losslessly under the InMemory
        // provider used by Gate 5. InMemory-safe; no schema change (ADR-002).
        builder.Property(mp => mp.RoleName);
        builder.Property(mp => mp.Username);
        builder.Property(mp => mp.DisplayName);

        // MIGRATION: the 4 base-only scalars inherited from Permission have NO
        // column on the ModulePermission table, so they are Ignored on this derived
        // type (they remain mapped only on the Permission base table).
        builder.Ignore(mp => mp.PermissionCode);
        builder.Ignore(mp => mp.ModuleDefID);
        builder.Ignore(mp => mp.PermissionKey);
        builder.Ignore(mp => mp.PermissionName);

        // MIGRATION: no relationship is configured from this side. The
        // Module -> ModulePermissions relationship is declared on the principal
        // side in ModuleConfiguration; ModuleID and PermissionID remain scalar FK
        // columns and there is no Permission navigation property on ModulePermission.
    }

    /// <summary>
    /// Configures the <see cref="TabPermission"/> junction against the existing
    /// SINGULAR <c>dbo.TabPermission</c> table (primary key <c>TabPermissionID</c>),
    /// detaching it from the <see cref="Permission"/> EF inheritance hierarchy so it
    /// maps to its own table. Structurally identical to <see cref="ModulePermission"/>
    /// except the owning foreign key is <c>TabID</c>.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="TabPermission"/>.</param>
    public void Configure(EntityTypeBuilder<TabPermission> builder)
    {
        // MIGRATION (inheritance break): TabPermission is a CLR subclass of
        // Permission, but the DB stores it in its OWN table with its OWN identity
        // PK (TabPermissionID) plus a PermissionID FK — not TPH/TPT. Detach it from
        // the EF inheritance hierarchy so it maps as an independent root entity.
        // The (Type?) cast is MANDATORY: a bare HasBaseType(null) is ambiguous
        // between the (string?) and (Type?) overloads (CS0121).
        builder.HasBaseType((Type?)null);

        // Map onto the pre-existing physical table; schema "dbo", SINGULAR name.
        builder.ToTable("TabPermission", "dbo");

        // Own primary key (NOT the inherited PermissionID). TabPermissionID is
        // IDENTITY(1,1); the convention default ValueGeneratedOnAdd is preserved.
        builder.HasKey(tp => tp.TabPermissionID);

        // Real physical columns, verified from AddTabPermission / UpdateTabPermission
        // (SqlDataProvider.vb) and the CREATE TABLE DDL.
        builder.Property(tp => tp.TabPermissionID);     // [TabPermissionID] int NOT NULL IDENTITY(1,1)
        builder.Property(tp => tp.TabID);               // [TabID] int NOT NULL (scalar FK; relationship in TabConfiguration)

        // MIGRATION: PermissionID is INHERITED from the Permission base type but is
        // a REAL FK column on the TabPermission table — it is MAPPED here (NOT
        // Ignored). It stays a plain scalar int; no EF relationship is configured.
        builder.Property(tp => tp.PermissionID);        // [PermissionID] int NOT NULL (FK -> Permission)

        // MIGRATION: DB RoleID/UserID are NULLABLE in the final DNN 4.9 schema
        // (procs use GetRoleNull(roleID) / GetNull(UserID), -1 / Null.NullInteger
        // sentinel). The Domain entity keeps them as non-nullable int per ADR-002,
        // so they are mapped AS-IS. Do NOT add .IsRequired(false) to a non-nullable int.
        builder.Property(tp => tp.RoleID);              // [RoleID] int (nullable in DB; non-nullable in entity)
        builder.Property(tp => tp.AllowAccess);         // [AllowAccess] bit NOT NULL
        builder.Property(tp => tp.UserID);              // [UserID] int (nullable in DB; added by post-baseline upgrade)

        // MIGRATION: RoleName / Username / DisplayName are NOT physical
        // TabPermission columns — legacy code populated them via JOINs to the
        // Roles / Users tables. They are carried as plain scalar properties (NOT
        // Ignored) for lossless round-trip under the InMemory provider (Gate 5).
        builder.Property(tp => tp.RoleName);
        builder.Property(tp => tp.Username);
        builder.Property(tp => tp.DisplayName);

        // MIGRATION: the 4 base-only scalars inherited from Permission have NO
        // column on the TabPermission table, so they are Ignored on this derived
        // type (they remain mapped only on the Permission base table).
        builder.Ignore(tp => tp.PermissionCode);
        builder.Ignore(tp => tp.ModuleDefID);
        builder.Ignore(tp => tp.PermissionKey);
        builder.Ignore(tp => tp.PermissionName);

        // MIGRATION: no relationship is configured from this side. The
        // Tab -> TabPermissions relationship is declared on the principal side in
        // TabConfiguration; TabID and PermissionID remain scalar FK columns and
        // there is no Permission navigation property on TabPermission.
    }

    /// <summary>
    /// Configures the <see cref="FolderPermission"/> junction against the existing
    /// SINGULAR <c>dbo.FolderPermission</c> table (primary key
    /// <c>FolderPermissionID</c>), detaching it from the <see cref="Permission"/>
    /// EF inheritance hierarchy so it maps to its own table.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="FolderPermission"/>.</param>
    public void Configure(EntityTypeBuilder<FolderPermission> builder)
    {
        // MIGRATION (inheritance break): FolderPermission is a CLR subclass of
        // Permission, but the DB stores it in its OWN table with its OWN identity
        // PK (FolderPermissionID) plus a PermissionID FK — not TPH/TPT. Detach it
        // from the EF inheritance hierarchy so it maps as an independent root
        // entity. The (Type?) cast is MANDATORY: a bare HasBaseType(null) is
        // ambiguous between the (string?) and (Type?) overloads (CS0121).
        builder.HasBaseType((Type?)null);

        // Map onto the pre-existing physical table; schema "dbo", SINGULAR name.
        builder.ToTable("FolderPermission", "dbo");

        // Own primary key (NOT the inherited PermissionID). FolderPermissionID is
        // IDENTITY(1,1); the convention default ValueGeneratedOnAdd is preserved.
        builder.HasKey(fp => fp.FolderPermissionID);

        // Real physical columns, verified from AddFolderPermission /
        // UpdateFolderPermission (SqlDataProvider.vb) and the CREATE TABLE DDL.
        builder.Property(fp => fp.FolderPermissionID);  // [FolderPermissionID] int NOT NULL IDENTITY(1,1)
        builder.Property(fp => fp.FolderID);            // [FolderID] int NOT NULL

        // MIGRATION: PermissionID is INHERITED from the Permission base type but is
        // a REAL FK column on the FolderPermission table — it is MAPPED here (NOT
        // Ignored). It stays a plain scalar int; no EF relationship is configured.
        builder.Property(fp => fp.PermissionID);        // [PermissionID] int NOT NULL (FK -> Permission)

        // MIGRATION: DB RoleID/UserID are NULLABLE in the final DNN 4.9 schema
        // (procs use GetRoleNull(roleID) / GetNull(UserID), -1 / Null.NullInteger
        // sentinel). The Domain entity keeps them as non-nullable int per ADR-002,
        // so they are mapped AS-IS. Do NOT add .IsRequired(false) to a non-nullable int.
        builder.Property(fp => fp.RoleID);              // [RoleID] int (nullable in DB; non-nullable in entity)
        builder.Property(fp => fp.AllowAccess);         // [AllowAccess] bit NOT NULL
        builder.Property(fp => fp.UserID);              // [UserID] int (nullable in DB; added by post-baseline upgrade)

        // MIGRATION: PortalID and FolderPath are NOT columns on the FolderPermission
        // table — in the legacy schema they live on the Folders table and were
        // JOIN-populated (the AddFolderPermission proc takes neither). Together with
        // the display-only RoleName / Username / DisplayName lookups, they are
        // carried as plain scalar properties (NOT Ignored, NOT mapped to
        // non-existent physical columns) so a FolderPermission round-trips
        // losslessly under the InMemory provider used by Gate 5. InMemory-safe; no
        // schema change (ADR-002).
        builder.Property(fp => fp.PortalID);
        builder.Property(fp => fp.FolderPath);
        builder.Property(fp => fp.RoleName);
        builder.Property(fp => fp.Username);
        builder.Property(fp => fp.DisplayName);

        // MIGRATION: the 4 base-only scalars inherited from Permission have NO
        // column on the FolderPermission table, so they are Ignored on this derived
        // type (they remain mapped only on the Permission base table).
        builder.Ignore(fp => fp.PermissionCode);
        builder.Ignore(fp => fp.ModuleDefID);
        builder.Ignore(fp => fp.PermissionKey);
        builder.Ignore(fp => fp.PermissionName);

        // MIGRATION: no relationship is configured here. FolderPermission has no
        // navigation properties and there is no Folder entity in scope, so FolderID,
        // PermissionID and PortalID remain plain scalar columns.
    }
}
