using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// =============================================================================================
// MIGRATION: TabConfiguration — EF Core 8 Fluent API mapping for the DotNetNuke 4.9.0.85 Tab
// aggregate (in DNN a "Tab" == a site Page). This single class is the SINGLE HOME for that mapping
// and is auto-discovered by DnnDbContext.OnModelCreating via
// modelBuilder.ApplyConfigurationsFromAssembly(...). It replaces the legacy ADO.NET / SqlDataProvider
// stored-procedure layer (AddTab / UpdateTab / GetTab / GetTabs in
// Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb) together with the
// reflection-based CBO.FillObject hydration, mapping the Tab POCO onto the pre-existing, UNCHANGED
// dbo.Tabs table.
//
// SCHEMA SOURCE OF TRUTH: the dbo.Tabs CREATE TABLE DDL in
// Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider (PK [TabID]
// CLUSTERED, IDENTITY(0,1)) plus the [IsSecure] column added by 04.05.04.SqlDataProvider (ALTER TABLE
// Tabs ADD IsSecure bit NOT NULL) and confirmed live by 04.09.00.SqlDataProvider
// (update Tabs set IsSecure = 1). 22 physical columns total.
//
// ---------------------------------------------------------------------------------------------
// DECISION 1 — Ignore(TabType). Tab.TabType is a COMPUTED, read-only enum property (legacy
//   TabInfo.vb L406, <XmlIgnore()> Public ReadOnly Property TabType) derived from Url at runtime; it
//   has no setter and no backing field. EF would otherwise attempt to materialize it and the model
//   build fails, so it is explicitly excluded from the EF model.
//
// DECISION 2 — IsDeleted soft-delete PRESERVED. [IsDeleted] (bit NOT NULL) is a real column and the
//   soft-delete flag that TabService/TabRepository filter on; it is MAPPED (never ignored).
//
// DECISION 3 — ParentId nullable self-FK as a SCALAR. [ParentId] (int NULL) references Tabs.TabID,
//   but the Tab entity exposes NO Parent/Children navigation, so it is mapped as a plain nullable
//   scalar with NO EF self-relationship (faithful to the legacy TabInfo value object).
//
// DECISION 4 (DEV-031 — CP2 schema-fidelity correction) — three non-physical properties IGNORED.
//   HasChildren / AuthorizedRoles / AdministratorRoles were computed / permission-derived at runtime in
//   legacy (they appear only as stored-procedure @parameters, never as Tabs columns). Per ADR-002 they
//   are Ignore()d — NOT carried as scalar columns — so EF never references nonexistent Tabs columns (the
//   prior scalar mapping violated ADR-002 and would fail against SQL Server). The CLR auto-properties
//   remain for projection; they are populated via tab-hierarchy / permission projections in the
//   repository/service layer in a later checkpoint.
//
// DECISION 5 — Tab (principal) -> TabPermissions (dependent) relationship. TabPermission is mapped as
//   an independent root entity in PermissionConfiguration (HasBaseType((Type?)null)); the principal
//   side of the Tab -> TabPermission relationship is declared HERE on TabConfiguration and EF merges
//   the two configurations. WithOne() = no inverse navigation; [TabID] is the FK on TabPermission.
//
// SCHEMA FIDELITY / InMemory SAFETY (ADR-002): the schema is mapped UNCHANGED — no migrations, no
// schema generation, no EnsureCreated, no data migration. To keep the model fully compatible with the
// Microsoft.EntityFrameworkCore.InMemory provider used by the integration-test suite (Gate 5), NO
// relational-only constructs are configured (no HasDefaultValueSql, no HasComputedColumnSql, no
// HasColumnType, no raw SQL) and the integer identity key is left at the EF ValueGeneratedOnAdd
// convention (ValueGeneratedNever() is intentionally NOT called) so the provider can synthesize keys
// on insert. All five decisions above are also recorded in the root MIGRATION_NOTES.md.
// =============================================================================================

/// <summary>
/// Entity Framework Core 8 Fluent API configuration that maps the <see cref="Tab"/> domain POCO
/// (a DotNetNuke "Tab" == a site Page) onto the pre-existing, UNCHANGED DotNetNuke <c>4.9.0.85</c>
/// <c>[dbo].[Tabs]</c> table, and declares the principal side of the <see cref="Tab"/> →
/// <see cref="TabPermission"/> relationship.
/// </summary>
/// <remarks>
/// <para>
/// This is the SINGLE HOME for the EF mapping of <see cref="Tab"/>. The Domain entity carries zero
/// framework attributes (Clean Architecture inner ring); every table name, key, and column is
/// described here and auto-discovered by <c>DnnDbContext.OnModelCreating</c> via
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>. The
/// compiler-generated public parameterless constructor lets EF instantiate the class.
/// </para>
/// <para>
/// ADR-002 — the schema is mapped UNCHANGED: no EF migrations, no schema generation, no
/// <c>EnsureCreated</c>, and no data migration. To keep the model compatible with the
/// <c>Microsoft.EntityFrameworkCore.InMemory</c> provider used by the integration-test suite
/// (Gate 5), no relational-only constructs are configured (no <c>HasDefaultValueSql</c>,
/// <c>HasComputedColumnSql</c>, <c>HasColumnType</c>, or raw SQL) and the identity key is left at the
/// EF <c>ValueGeneratedOnAdd</c> convention. See the file-level <c>// MIGRATION</c> banner above for
/// the five documented mapping decisions.
/// </para>
/// </remarks>
public sealed class TabConfiguration : IEntityTypeConfiguration<Tab>
{
    /// <summary>
    /// Configures the <see cref="Tab"/> entity against the existing DotNetNuke <c>[dbo].[Tabs]</c>
    /// table (default install qualification <c>{databaseOwner}[{objectQualifier}Tabs]</c>): the table
    /// and schema, the <c>TabID</c> primary key, the 22 physical columns (including the nullable
    /// <c>ParentId</c> self-FK scalar), the three carried non-physical scalars, the mandatory
    /// <c>Ignore</c> of the computed <c>TabType</c>, and the principal side of the
    /// <c>Tab → TabPermission</c> relationship. Column names are taken verbatim from the schema DDL,
    /// so no <c>HasColumnName</c> is required (ADR-002).
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="Tab"/>.</param>
    public void Configure(EntityTypeBuilder<Tab> builder)
    {
        // Map onto the pre-existing, unchanged DNN 4.9 table (ADR-002). The {objectQualifier} tenant
        // prefix is expressed through ToTable rather than string-concatenated at every call site.
        builder.ToTable("Tabs", "dbo");

        // Primary key [TabID] (int NOT NULL IDENTITY(0,1), PK CLUSTERED). Key generation is left at the
        // EF ValueGeneratedOnAdd convention (NOT ValueGeneratedNever) so the database IDENTITY is
        // honored on SQL Server and key values are still synthesized under the InMemory provider
        // (Gate 5).
        builder.HasKey(t => t.TabID);

        // MIGRATION: Tab.TabType is a computed read-only enum (no column/backing field); Ignore() to
        // keep it out of the EF model. It is derived from Url at runtime (legacy TabInfo.vb L406,
        // <XmlIgnore()> ReadOnly Property TabType); mapping it would fail the model build because EF
        // cannot materialize a get-only property that has no setter and no backing field.
        builder.Ignore(t => t.TabType);

        // ---- Physical [Tabs] columns (verbatim DNN 4.9 schema names, DDL order; default type mapping) ----
        // Entity property name == physical column name for every column, so no HasColumnName is
        // required. Declared explicitly to serve as the authoritative column manifest; EF default type
        // mapping is used throughout (no HasColumnType / HasMaxLength / HasDefaultValueSql) to keep the
        // model InMemory-safe (Gate 5), mirroring the PermissionConfiguration precedent.
        builder.Property(t => t.TabID);            // [TabID]           int           NOT NULL IDENTITY(0,1)
        builder.Property(t => t.TabOrder);         // [TabOrder]        int           NOT NULL DEFAULT 0

        // MIGRATION: [PortalID] is physically NULLABLE (int NULL) in the DNN 4.9 schema, but the Tab
        // entity models it as a non-nullable int (host/super tabs are handled at the service layer, not
        // by nulling the FK). Per ADR-002 the entity is mapped AS-IS; .IsRequired(false) is NOT applied.
        builder.Property(t => t.PortalID);         // [PortalID]        int           NULL (non-nullable int entity; scalar FK -> Portals)

        builder.Property(t => t.TabName);          // [TabName]         nvarchar(50)  NOT NULL
        builder.Property(t => t.IsVisible);        // [IsVisible]       bit           NOT NULL DEFAULT 1

        // MIGRATION: Tabs.ParentId is a nullable self-FK to Tabs.TabID; no Parent/Children navigation on
        // the entity, so mapped as a scalar (matches legacy TabInfo). NO EF self-relationship is
        // configured — ParentId remains a plain nullable int column.
        builder.Property(t => t.ParentId);         // [ParentId]        int           NULL (nullable self-FK scalar)

        builder.Property(t => t.Level);            // [Level]           int           NOT NULL DEFAULT 0
        builder.Property(t => t.IconFile);         // [IconFile]        nvarchar(100) NULL
        builder.Property(t => t.DisableLink);      // [DisableLink]     bit           NOT NULL DEFAULT 0
        builder.Property(t => t.Title);            // [Title]           nvarchar(200) NULL
        builder.Property(t => t.Description);      // [Description]     nvarchar(500) NULL
        builder.Property(t => t.KeyWords);         // [KeyWords]        nvarchar(500) NULL

        // MIGRATION: [IsDeleted] (bit NOT NULL) soft-delete flag PRESERVED (recycle-bin semantics;
        // TabService/TabRepository list reads filter on it). MAPPED — never ignored.
        builder.Property(t => t.IsDeleted);        // [IsDeleted]       bit           NOT NULL DEFAULT 0

        builder.Property(t => t.Url);              // [Url]             nvarchar(255) NULL
        builder.Property(t => t.SkinSrc);          // [SkinSrc]         nvarchar(200) NULL
        builder.Property(t => t.ContainerSrc);     // [ContainerSrc]    nvarchar(200) NULL
        builder.Property(t => t.TabPath);          // [TabPath]         nvarchar(255) NULL
        builder.Property(t => t.StartDate);        // [StartDate]       datetime      NULL (DateTime?)
        builder.Property(t => t.EndDate);          // [EndDate]         datetime      NULL (DateTime?)
        builder.Property(t => t.RefreshInterval);  // [RefreshInterval] int           NULL (non-nullable int entity)
        builder.Property(t => t.PageHeadText);     // [PageHeadText]    nvarchar(500) NULL

        // MIGRATION: [IsSecure] (bit NOT NULL) IS a real column in the 4.9 schema — added by
        // 04.05.04.SqlDataProvider (ALTER TABLE Tabs ADD IsSecure) and confirmed by 04.09.00
        // (update Tabs set IsSecure = 1). MAPPED.
        builder.Property(t => t.IsSecure);         // [IsSecure]        bit           NOT NULL

        // MIGRATION (DEV-031 — CP2 schema-fidelity correction): HasChildren / AuthorizedRoles /
        // AdministratorRoles were computed / permission-derived at runtime (NOT physical Tabs columns —
        // they appear only as stored-procedure @parameters). Per ADR-002 they are Ignore()d — NOT carried
        // as scalar columns — so EF never queries/inserts nonexistent Tabs columns. The CLR auto-properties
        // remain for projection and are populated via tab-hierarchy / permission projections in the
        // repository/service layer in a later checkpoint.
        builder.Ignore(t => t.HasChildren);          // computed in legacy (no Tabs column)
        builder.Ignore(t => t.AuthorizedRoles);      // permission-derived in legacy (no Tabs column)
        builder.Ignore(t => t.AdministratorRoles);   // permission-derived in legacy (no Tabs column)

        // MIGRATION: Tab (principal) -> TabPermissions (dependent). TabPermission is configured as an
        // independent root entity in PermissionConfiguration (HasBaseType((Type?)null)); the principal
        // side of the relationship is declared HERE on TabConfiguration and EF merges the two
        // configurations.
        //   * WithOne()                        — no inverse navigation on TabPermission (it exposes no
        //                                         Tab navigation property).
        //   * HasForeignKey(tp => tp.TabID)    — [TabID] is the FK column on TabPermission (already
        //                                         mapped as a scalar by PermissionConfiguration and
        //                                         reused here as the relationship's foreign key).
        //   * OnDelete(DeleteBehavior.NoAction) — Tab uses SOFT delete (IsDeleted), so a hard cascade is
        //     not the legacy behavior; NoAction is faithful and also avoids a SQL-Server multiple-
        //     cascade-path model warning under the --warnaserror gate. The InMemory provider ignores
        //     delete behavior, so this is safe for Gate 5.
        builder.HasMany(t => t.TabPermissions)
            .WithOne()
            .HasForeignKey(tp => tp.TabID)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
