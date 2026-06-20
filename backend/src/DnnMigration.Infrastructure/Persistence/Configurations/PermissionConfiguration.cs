using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// =============================================================================================
// MIGRATION: PermissionConfiguration — EF Core 8 Fluent API mapping for the DotNetNuke 4.9.0.85
// permission aggregate (Permission + the three junction tables ModulePermission, TabPermission and
// FolderPermission). This single class is the SINGLE HOME for that mapping and is auto-discovered by
// DnnDbContext.OnModelCreating via modelBuilder.ApplyConfigurationsFromAssembly(...). It replaces the
// legacy ADO.NET / SqlDataProvider stored-procedure layer (AddPermission / AddModulePermission /
// AddTabPermission / AddFolderPermission and their Update*/Get* counterparts in
// Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb) together with the
// reflection-based CBO.FillObject hydration.
//
// ---------------------------------------------------------------------------------------------
// DEVIATION 1 — EF inheritance hierarchy is deliberately BROKEN (HasBaseType((Type?)null)).
//   The Domain models the three junctions as C# subclasses of Permission
//   (FolderPermission : Permission, ModulePermission : Permission, TabPermission : Permission),
//   BUT the physical DNN schema is FOUR separate tables each with its OWN identity primary key
//   (Permission.PermissionID, ModulePermission.ModulePermissionID, TabPermission.TabPermissionID,
//   FolderPermission.FolderPermissionID) plus a PermissionID FOREIGN-KEY column on each junction.
//   That is neither Table-Per-Hierarchy nor Table-Per-Type, so EF's default inheritance convention
//   (which would fold all four CLR types into one Permission table with a discriminator) is WRONG.
//   Each derived Configure therefore calls builder.HasBaseType((Type?)null) FIRST to detach the type
//   from the Permission EF hierarchy and make it an independent root entity with its own table + PK.
//   The (Type?)null cast is MANDATORY: a bare HasBaseType(null) is ambiguous between the
//   HasBaseType(string?) and HasBaseType(Type?) overloads and fails to compile (CS0121).
//
// DEVIATION 2 — the four inherited base-only scalars (PermissionCode, ModuleDefID, PermissionKey,
//   PermissionName) are Ignore()d on every derived type. After the hierarchy is broken, EF would
//   otherwise try to map those inherited CLR properties as columns on the junction tables, where they
//   do NOT exist (they live only on the base Permission table). They are explicitly ignored so the
//   junction entities map to exactly their real columns.
//
// DEVIATION 3 — RoleID / UserID nullability nuance. In the final DNN 4.9 schema the junction-table
//   RoleID and UserID columns are NULLABLE (the legacy Add/Update procedures pass GetRoleNull(roleID)
//   and GetNull(UserID), substituting SQL NULL for the -1 / Null.NullInteger "all roles / all users"
//   sentinel). The Domain entities, however, model RoleID and UserID as non-nullable int, preserving
//   the legacy sentinel semantics. Per ADR-002 the entities are mapped AS-IS and are NOT changed, so
//   .IsRequired(false) is deliberately NOT applied to these non-nullable int properties.
//
// DEVIATION 4 (DEV-031 — CP2 schema-fidelity correction) — display-only / non-physical properties are
//   IGNORED. RoleName / Username / DisplayName on all three junctions (and additionally PortalID /
//   FolderPath on FolderPermission, which physically live on the Folders table in legacy, not
//   FolderPermission) were JOIN-populated lookups in the legacy value objects. Per ADR-002 they are
//   Ignore()d — NOT mapped (nor carried) as scalar columns — so EF never references columns that do not
//   physically exist on the junction tables (the prior scalar mapping violated ADR-002 and would fail
//   against SQL Server). The CLR properties remain on the entities for projection and are populated via
//   joins/projections in the repository/service layer in a later checkpoint.
//
// SCHEMA FIDELITY / InMemory SAFETY (ADR-002): the existing schema is mapped UNCHANGED — no EF
// migrations, no schema generation, no EnsureCreated, no data migration. All four physical table names
// are SINGULAR (Permission, ModulePermission, TabPermission, FolderPermission) even though the
// DnnDbContext DbSet accessors are pluralized. To keep the model fully compatible with the InMemory
// provider, NO relational-only constructs are configured (no HasDefaultValueSql, no
// HasComputedColumnSql, no HasColumnType, no raw SQL) and integer identity keys are left at the EF
// ValueGeneratedOnAdd convention (ValueGeneratedNever() is intentionally NOT called) so the provider
// can synthesize keys on insert. If this mapping is wrong the whole EF model fails to build and EVERY
// Gate 5 integration test fails, so correctness here is essential even though Permission itself is not
// directly CRUD-tested. These four deviations are also recorded in the root MIGRATION_NOTES.md; the
// // MIGRATION comments in this file are their authoritative source.
// =============================================================================================

/// <summary>
/// Entity Framework Core 8 Fluent API configuration that maps the DotNetNuke permission aggregate —
/// the base <see cref="Permission"/> descriptor and the three junction entities
/// <see cref="FolderPermission"/>, <see cref="ModulePermission"/> and <see cref="TabPermission"/> —
/// onto the pre-existing, UNCHANGED DotNetNuke <c>4.9.0.85</c> <c>dbo.Permission</c>,
/// <c>dbo.FolderPermission</c>, <c>dbo.ModulePermission</c> and <c>dbo.TabPermission</c> tables.
/// </summary>
/// <remarks>
/// <para>
/// A single class intentionally implements four <see cref="IEntityTypeConfiguration{TEntity}"/>
/// contracts; the four <c>Configure</c> methods are distinct overloads (different builder types) and
/// the assembly scan performed by <c>DnnDbContext.OnModelCreating</c> applies all four. The
/// compiler-generated public parameterless constructor lets EF instantiate the class.
/// </para>
/// <para>
/// The Domain models the three junctions as CLR subclasses of <see cref="Permission"/>, but the
/// physical schema uses four independent tables with four distinct identity primary keys plus a
/// <c>PermissionID</c> foreign-key column on each junction. Because that is neither TPH nor TPT, each
/// derived type's configuration first detaches itself from the EF inheritance hierarchy via
/// <c>builder.HasBaseType((Type?)null)</c> and is then mapped as an independent root entity. See the
/// file-level <c>// MIGRATION</c> banner above for the full rationale and the four documented
/// deviations.
/// </para>
/// </remarks>
public sealed class PermissionConfiguration :
    IEntityTypeConfiguration<Permission>,
    IEntityTypeConfiguration<FolderPermission>,
    IEntityTypeConfiguration<ModulePermission>,
    IEntityTypeConfiguration<TabPermission>
{
    /// <summary>
    /// Configures the base <see cref="Permission"/> entity against the physical, singular
    /// <c>dbo.Permission</c> table. This is the only one of the four types that is configured
    /// "normally": it is the EF hierarchy root, so <c>HasBaseType</c> is NOT called and no
    /// relationships to the derived junction types are declared (the Domain exposes no such
    /// navigation properties).
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="Permission"/>.</param>
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        // Map onto the pre-existing, unchanged DNN 4.9 table. The physical table name is SINGULAR
        // ("Permission"), preserved verbatim per ADR-002 even though the DbSet accessor is plural.
        builder.ToTable("Permission", "dbo");

        // Primary key [PermissionID] (int NOT NULL IDENTITY(1,1)). Key generation is left at the EF
        // ValueGeneratedOnAdd convention (NOT ValueGeneratedNever) so the database IDENTITY is honored
        // on SQL Server and key values are still synthesized under the InMemory provider (Gate 5).
        builder.HasKey(p => p.PermissionID);

        // ---- Physical [Permission] columns (verbatim DNN 4.9 schema names, DDL order) ----
        // Entity property name == physical column name for all five columns, so no HasColumnName is
        // required. Declared explicitly to serve as the authoritative column manifest; EF default type
        // mapping is used throughout (no HasColumnType / HasMaxLength / HasDefaultValueSql) to keep the
        // model InMemory-safe.
        builder.Property(p => p.PermissionID);    // [PermissionID]   int          NOT NULL IDENTITY(1,1)
        builder.Property(p => p.PermissionCode);  // [PermissionCode] varchar(50)  NOT NULL
        builder.Property(p => p.ModuleDefID);     // [ModuleDefID]    int          NOT NULL
        builder.Property(p => p.PermissionKey);   // [PermissionKey]  varchar(20)  NOT NULL
        builder.Property(p => p.PermissionName);  // [PermissionName] varchar(50)  NOT NULL
    }

    /// <summary>
    /// Configures the <see cref="FolderPermission"/> junction entity against the physical, singular
    /// <c>dbo.FolderPermission</c> table (PK <c>FolderPermissionID</c>). The CLR base type is detached
    /// first so the type maps as an independent root rather than as a member of the Permission EF
    /// hierarchy.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="FolderPermission"/>.</param>
    public void Configure(EntityTypeBuilder<FolderPermission> builder)
    {
        // MIGRATION (Deviation 1): detach from the Permission EF hierarchy so FolderPermission maps to
        // its OWN table with its OWN identity PK. The (Type?)null cast is MANDATORY to disambiguate the
        // HasBaseType(string?) / HasBaseType(Type?) overloads (a bare null fails with CS0121). This call
        // MUST be first.
        builder.HasBaseType((Type?)null);

        // Physical table is SINGULAR "FolderPermission" (verbatim, ADR-002).
        builder.ToTable("FolderPermission", "dbo");

        // PK [FolderPermissionID] (int NOT NULL IDENTITY(1,1)); ValueGeneratedOnAdd convention retained.
        builder.HasKey(fp => fp.FolderPermissionID);

        // ---- Real physical [FolderPermission] columns (verbatim names; default type mapping) ----
        builder.Property(fp => fp.FolderPermissionID); // [FolderPermissionID] int  NOT NULL IDENTITY(1,1)
        builder.Property(fp => fp.FolderID);           // [FolderID]           int  NOT NULL  (scalar FK -> Folders)

        // MIGRATION (Deviation 1, FK side): [PermissionID] is the REAL foreign-key column on this
        // junction table (inherited from the Permission CLR base). It is MAPPED as a plain scalar — NOT
        // ignored — and no EF relationship/navigation is configured (the Domain exposes none).
        builder.Property(fp => fp.PermissionID);       // [PermissionID]       int  NOT NULL  (scalar FK -> Permission)

        // MIGRATION (Deviation 3): [RoleID] and [UserID] are NULLABLE in the DNN 4.9 DB (the legacy
        // Add/Update procs pass GetRoleNull(roleID)/GetNull(UserID) to substitute NULL for the
        // -1 / Null.NullInteger sentinel), but the Domain models them as non-nullable int to preserve
        // the sentinel semantics. Per ADR-002 they are mapped AS-IS; .IsRequired(false) is NOT applied.
        builder.Property(fp => fp.RoleID);             // [RoleID]      int     (nullable in DB; non-nullable int entity)
        builder.Property(fp => fp.AllowAccess);        // [AllowAccess] bit     NOT NULL
        builder.Property(fp => fp.UserID);             // [UserID]      int     (nullable in DB; non-nullable int entity)

        // MIGRATION (Deviation 4 — DEV-031 CP2 schema-fidelity correction): PortalID and FolderPath are
        // NOT physical FolderPermission columns — in legacy they live on the Folders table and were
        // JOIN-populated onto FolderPermissionInfo. RoleName / Username / DisplayName are likewise
        // display-only JOIN lookups. Per ADR-002 all are Ignore()d — NOT carried as scalar columns — so EF
        // never references nonexistent FolderPermission columns. The CLR properties remain for projection
        // and are populated via joins in the repository/service layer in a later checkpoint.
        builder.Ignore(fp => fp.PortalID);           // no FolderPermission column (physically on Folders in legacy)
        builder.Ignore(fp => fp.FolderPath);         // no FolderPermission column (physically on Folders in legacy)
        builder.Ignore(fp => fp.RoleName);           // display-only (JOIN-populated in legacy)
        builder.Ignore(fp => fp.Username);           // display-only (JOIN-populated in legacy)
        builder.Ignore(fp => fp.DisplayName);        // display-only (JOIN-populated in legacy)

        // MIGRATION (Deviation 2): Ignore the four inherited base-only scalars — they belong to the
        // Permission base table and have NO column on FolderPermission.
        builder.Ignore(fp => fp.PermissionCode);
        builder.Ignore(fp => fp.ModuleDefID);
        builder.Ignore(fp => fp.PermissionKey);
        builder.Ignore(fp => fp.PermissionName);

        // MIGRATION: no relationship is configured here. FolderPermission has no navigation properties
        // and there is no Folder entity in scope; FolderID and PermissionID remain plain scalar FKs.
    }

    /// <summary>
    /// Configures the <see cref="ModulePermission"/> junction entity against the physical, singular
    /// <c>dbo.ModulePermission</c> table (PK <c>ModulePermissionID</c>). The CLR base type is detached
    /// first so the type maps as an independent root rather than as a member of the Permission EF
    /// hierarchy.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="ModulePermission"/>.</param>
    public void Configure(EntityTypeBuilder<ModulePermission> builder)
    {
        // MIGRATION (Deviation 1): detach from the Permission EF hierarchy so ModulePermission maps to
        // its OWN table with its OWN identity PK. The (Type?)null cast is MANDATORY to disambiguate the
        // HasBaseType(string?) / HasBaseType(Type?) overloads (a bare null fails with CS0121). This call
        // MUST be first.
        builder.HasBaseType((Type?)null);

        // Physical table is SINGULAR "ModulePermission" (verbatim, ADR-002).
        builder.ToTable("ModulePermission", "dbo");

        // PK [ModulePermissionID] (int NOT NULL IDENTITY(1,1)); ValueGeneratedOnAdd convention retained.
        builder.HasKey(mp => mp.ModulePermissionID);

        // ---- Real physical [ModulePermission] columns (verbatim names; default type mapping) ----
        builder.Property(mp => mp.ModulePermissionID); // [ModulePermissionID] int NOT NULL IDENTITY(1,1)
        builder.Property(mp => mp.ModuleID);           // [ModuleID]           int NOT NULL  (scalar FK -> Modules)

        // MIGRATION (Deviation 1, FK side): [PermissionID] is the REAL foreign-key column on this
        // junction table (inherited from the Permission CLR base). It is MAPPED as a plain scalar — NOT
        // ignored — and no EF relationship/navigation is configured.
        builder.Property(mp => mp.PermissionID);       // [PermissionID]       int NOT NULL  (scalar FK -> Permission)

        // MIGRATION (Deviation 3): [RoleID] and [UserID] are NULLABLE in the DNN 4.9 DB (the legacy
        // AddModulePermission/UpdateModulePermission procs pass GetRoleNull(roleID)/GetNull(UserID) to
        // substitute NULL for the -1 / Null.NullInteger sentinel), but the Domain models them as
        // non-nullable int to preserve the sentinel semantics. Per ADR-002 they are mapped AS-IS;
        // .IsRequired(false) is NOT applied.
        builder.Property(mp => mp.RoleID);             // [RoleID]      int     (nullable in DB; non-nullable int entity)
        builder.Property(mp => mp.AllowAccess);        // [AllowAccess] bit     NOT NULL
        builder.Property(mp => mp.UserID);             // [UserID]      int     (nullable in DB; non-nullable int entity)

        // MIGRATION (Deviation 4 — DEV-031 CP2 schema-fidelity correction): RoleName / Username /
        // DisplayName are display-only JOIN lookups in the legacy ModulePermissionInfo value object, NOT
        // physical columns. Per ADR-002 they are Ignore()d — NOT carried as scalar columns — so EF never
        // references nonexistent ModulePermission columns. The CLR properties remain for projection and are
        // populated via joins in the repository/service layer in a later checkpoint.
        builder.Ignore(mp => mp.RoleName);           // display-only (JOIN-populated in legacy)
        builder.Ignore(mp => mp.Username);           // display-only (JOIN-populated in legacy)
        builder.Ignore(mp => mp.DisplayName);        // display-only (JOIN-populated in legacy)

        // MIGRATION (Deviation 2): Ignore the four inherited base-only scalars — they belong to the
        // Permission base table and have NO column on ModulePermission.
        builder.Ignore(mp => mp.PermissionCode);
        builder.Ignore(mp => mp.ModuleDefID);
        builder.Ignore(mp => mp.PermissionKey);
        builder.Ignore(mp => mp.PermissionName);

        // MIGRATION: no relationship is configured here. The Module -> ModulePermissions relationship is
        // declared on the principal side in ModuleConfiguration; ModulePermission has no Permission
        // navigation, so ModuleID and PermissionID remain plain scalar FK columns here.
    }

    /// <summary>
    /// Configures the <see cref="TabPermission"/> junction entity against the physical, singular
    /// <c>dbo.TabPermission</c> table (PK <c>TabPermissionID</c>). The CLR base type is detached first
    /// so the type maps as an independent root rather than as a member of the Permission EF hierarchy.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="TabPermission"/>.</param>
    public void Configure(EntityTypeBuilder<TabPermission> builder)
    {
        // MIGRATION (Deviation 1): detach from the Permission EF hierarchy so TabPermission maps to its
        // OWN table with its OWN identity PK. The (Type?)null cast is MANDATORY to disambiguate the
        // HasBaseType(string?) / HasBaseType(Type?) overloads (a bare null fails with CS0121). This call
        // MUST be first.
        builder.HasBaseType((Type?)null);

        // Physical table is SINGULAR "TabPermission" (verbatim, ADR-002).
        builder.ToTable("TabPermission", "dbo");

        // PK [TabPermissionID] (int NOT NULL IDENTITY(1,1)); ValueGeneratedOnAdd convention retained.
        builder.HasKey(tp => tp.TabPermissionID);

        // ---- Real physical [TabPermission] columns (verbatim names; default type mapping) ----
        builder.Property(tp => tp.TabPermissionID);    // [TabPermissionID] int NOT NULL IDENTITY(1,1)
        builder.Property(tp => tp.TabID);              // [TabID]           int NOT NULL  (scalar FK -> Tabs)

        // MIGRATION (Deviation 1, FK side): [PermissionID] is the REAL foreign-key column on this
        // junction table (inherited from the Permission CLR base). It is MAPPED as a plain scalar — NOT
        // ignored — and no EF relationship/navigation is configured.
        builder.Property(tp => tp.PermissionID);       // [PermissionID]    int NOT NULL  (scalar FK -> Permission)

        // MIGRATION (Deviation 3): [RoleID] and [UserID] are NULLABLE in the DNN 4.9 DB (the legacy
        // AddTabPermission/UpdateTabPermission procs pass GetRoleNull(roleID)/GetNull(UserID) to
        // substitute NULL for the -1 / Null.NullInteger sentinel), but the Domain models them as
        // non-nullable int to preserve the sentinel semantics. Per ADR-002 they are mapped AS-IS;
        // .IsRequired(false) is NOT applied.
        builder.Property(tp => tp.RoleID);             // [RoleID]      int     (nullable in DB; non-nullable int entity)
        builder.Property(tp => tp.AllowAccess);        // [AllowAccess] bit     NOT NULL
        builder.Property(tp => tp.UserID);             // [UserID]      int     (nullable in DB; non-nullable int entity)

        // MIGRATION (Deviation 4 — DEV-031 CP2 schema-fidelity correction): RoleName / Username /
        // DisplayName are display-only JOIN lookups in the legacy TabPermissionInfo value object, NOT
        // physical columns. Per ADR-002 they are Ignore()d — NOT carried as scalar columns — so EF never
        // references nonexistent TabPermission columns. The CLR properties remain for projection and are
        // populated via joins in the repository/service layer in a later checkpoint.
        builder.Ignore(tp => tp.RoleName);           // display-only (JOIN-populated in legacy)
        builder.Ignore(tp => tp.Username);           // display-only (JOIN-populated in legacy)
        builder.Ignore(tp => tp.DisplayName);        // display-only (JOIN-populated in legacy)

        // MIGRATION (Deviation 2): Ignore the four inherited base-only scalars — they belong to the
        // Permission base table and have NO column on TabPermission.
        builder.Ignore(tp => tp.PermissionCode);
        builder.Ignore(tp => tp.ModuleDefID);
        builder.Ignore(tp => tp.PermissionKey);
        builder.Ignore(tp => tp.PermissionName);

        // MIGRATION: no relationship is configured here. The Tab -> TabPermissions relationship is
        // declared on the principal side in TabConfiguration; TabPermission has no Permission
        // navigation, so TabID and PermissionID remain plain scalar FK columns here.
    }
}
