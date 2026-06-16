// =============================================================================
// TabConfiguration.cs
// -----------------------------------------------------------------------------
// EF Core 8 Fluent API mapping for the Tab aggregate (a DotNetNuke "Tab" == a
// site Page) onto the EXISTING, UNCHANGED DotNetNuke 4.9.0.85 [dbo].[Tabs] table.
//
// MIGRATION: This configuration replaces the legacy ADO.NET / SqlDataProvider
// stored-procedure data layer (Library/Providers/DataProviders/SqlDataProvider/
// SqlDataProvider.vb -- AddTab / UpdateTab / GetTab / GetTabs) and the
// reflection-based CBO.FillObject hydration used by TabController.vb. The Tab
// POCO entity (DnnMigration.Domain.Entities) carries ZERO EF attributes; ALL
// persistence mapping lives here and is auto-discovered by
// DnnDbContext.OnModelCreating via ApplyConfigurationsFromAssembly. The
// principal side of the Tab -> TabPermission relationship is also declared here;
// the dependent TabPermission is detached from the Permission inheritance
// hierarchy and mapped as an independent root entity in PermissionConfiguration
// (HasBaseType((Type?)null)), and EF merges the two configurations.
//
// ADR-002 (schema preservation): the schema is mapped UNCHANGED -- no EF
// migrations, no schema generation, no SQL-Server-only defaults, no data
// migration. Table and column names are reproduced verbatim from the baseline
// install DDL (Website/Providers/DataProviders/SqlDataProvider/
// DotNetNuke.Schema.SqlDataProvider -- the [Tabs] CREATE TABLE) plus the
// 04.05.04 upgrade that adds [IsSecure]; nullability and lengths are
// cross-checked against the AddTab / UpdateTab stored-procedure call sites
// (GetNull(...)). Every mapping primitive used here (ToTable / HasKey /
// Property / HasMaxLength / Ignore / relationship metadata) is InMemory-provider
// safe, so the same model builds cleanly under both the SQL Server provider
// (production) and the EF Core InMemory provider used by the integration tests
// (Gate 5), which round-trip Tab CRUD.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core <see cref="IEntityTypeConfiguration{TEntity}"/> that maps the
/// <see cref="Tab"/> POCO entity onto its pre-existing DotNetNuke 4.9.0.85
/// <c>dbo.Tabs</c> table (in DotNetNuke a "Tab" is a site page) using the Fluent
/// API only, and configures the principal side of the one-to-many
/// <see cref="Tab"/> &#8594; <see cref="TabPermission"/> relationship.
/// </summary>
/// <remarks>
/// <para>
/// Discovered automatically by <c>DnnDbContext.OnModelCreating</c> via
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>,
/// so it requires no explicit registration. The <see cref="Tab"/> entity carries
/// no EF attributes; this class is the single home for its persistence metadata.
/// </para>
/// <para>
/// Per ADR-002 the schema is mapped exactly as it exists in the database -- there
/// are no EF migrations and no schema-generation side effects. Only
/// provider-agnostic relational metadata is configured (no
/// <c>HasDefaultValueSql</c>, <c>HasComputedColumnSql</c>, raw SQL, or
/// <c>ValueGeneratedNever</c>), so the same model builds cleanly under both the
/// SQL Server provider (production) and the EF Core InMemory provider
/// (integration-test fixtures, Gate 5). <c>TabID</c> is an <c>IDENTITY(0,1)</c>
/// column; leaving the integer-key convention intact preserves
/// <c>ValueGeneratedOnAdd</c> so the InMemory provider can auto-assign keys on
/// insert.
/// </para>
/// </remarks>
public sealed class TabConfiguration : IEntityTypeConfiguration<Tab>
{
    /// <summary>
    /// Configures the <see cref="Tab"/> entity against the existing
    /// <c>dbo.Tabs</c> table, reproducing the DotNetNuke 4.9.0.85 column set
    /// verbatim and wiring the principal side of the
    /// <see cref="Tab.TabPermissions"/> collection.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="Tab"/>.</param>
    public void Configure(EntityTypeBuilder<Tab> builder)
    {
        // Map onto the pre-existing physical table; schema "dbo", table name
        // reproduced verbatim (ADR-002 -- this only describes the mapping and
        // never triggers schema generation).
        builder.ToTable("Tabs", "dbo");

        // Primary key. [TabID] is an IDENTITY(0,1) column and the physical PK is
        // PK_Tabs PRIMARY KEY CLUSTERED ([TabID]). Leaving the int-PK convention
        // intact preserves ValueGeneratedOnAdd, so the InMemory provider can
        // auto-assign keys on insert -- required for the Tab POST -> 201
        // round-trip in Gate 5. (Do NOT call ValueGeneratedNever.)
        builder.HasKey(t => t.TabID);

        // MIGRATION: Tab.TabType is a computed read-only enum (no column/backing
        // field); Ignore() to keep it out of the EF model. The legacy
        // TabInfo.TabType (XmlIgnore, ReadOnly) returns Globals.GetURLType(_Url),
        // and the ported Tab entity re-expresses it as a get-only property. If EF
        // attempts to map it, the model build fails -- so this Ignore is REQUIRED.
        builder.Ignore(t => t.TabType);

        // ---------------------------------------------------------------------
        // Physical [Tabs] columns (verbatim DNN 4.9.0.85 schema names, ordered to
        // mirror the CREATE TABLE definition). Property names already equal their
        // column names, so HasColumnName is unnecessary; the explicit Property
        // declarations make this configuration the single, unambiguous source of
        // truth for the column set. nvarchar lengths reproduce the install DDL;
        // HasMaxLength is provider-agnostic metadata (InMemory-safe).
        // ---------------------------------------------------------------------
        builder.Property(t => t.TabID);                            // [TabID] int NOT NULL IDENTITY(0,1)
        builder.Property(t => t.TabOrder);                         // [TabOrder] int NOT NULL DEFAULT(0)
        builder.Property(t => t.PortalID);                         // [PortalID] int NULL (scalar FK; no navigation)
        builder.Property(t => t.TabName).HasMaxLength(50);         // [TabName] nvarchar(50) NOT NULL
        builder.Property(t => t.IsVisible);                        // [IsVisible] bit NOT NULL DEFAULT(1)

        // MIGRATION: Tabs.ParentId is a nullable self-FK to Tabs.TabID; no
        // Parent/Children navigation on the entity, so mapped as a scalar
        // (matches legacy TabInfo). No EF self-relationship is configured.
        builder.Property(t => t.ParentId);                         // [ParentId] int NULL (nullable self-FK, scalar)

        builder.Property(t => t.Level);                            // [Level] int NOT NULL DEFAULT(0)
        builder.Property(t => t.IconFile).HasMaxLength(100);       // [IconFile] nvarchar(100) NULL
        builder.Property(t => t.DisableLink);                      // [DisableLink] bit NOT NULL DEFAULT(0)
        builder.Property(t => t.Title).HasMaxLength(200);          // [Title] nvarchar(200) NULL
        builder.Property(t => t.Description).HasMaxLength(500);     // [Description] nvarchar(500) NULL
        builder.Property(t => t.KeyWords).HasMaxLength(500);       // [KeyWords] nvarchar(500) NULL

        // MIGRATION: soft-delete flag PRESERVED. [IsDeleted] bit NOT NULL is a
        // REAL column (NOT ignored); it is the soft-delete sentinel that
        // TabService / TabRepository filter on (DNN tabs are logically deleted,
        // not physically removed).
        builder.Property(t => t.IsDeleted);                        // [IsDeleted] bit NOT NULL DEFAULT(0)

        builder.Property(t => t.Url).HasMaxLength(255);            // [Url] nvarchar(255) NULL
        builder.Property(t => t.SkinSrc).HasMaxLength(200);        // [SkinSrc] nvarchar(200) NULL
        builder.Property(t => t.ContainerSrc).HasMaxLength(200);    // [ContainerSrc] nvarchar(200) NULL
        builder.Property(t => t.TabPath).HasMaxLength(255);        // [TabPath] nvarchar(255) NULL
        builder.Property(t => t.StartDate);                        // [StartDate] datetime NULL (DateTime?)
        builder.Property(t => t.EndDate);                          // [EndDate] datetime NULL (DateTime?)
        builder.Property(t => t.RefreshInterval);                  // [RefreshInterval] int NULL
        builder.Property(t => t.PageHeadText).HasMaxLength(500);    // [PageHeadText] nvarchar(500) NULL

        // MIGRATION: [IsSecure] bit NOT NULL is a REAL column in the final 4.9
        // schema. It is absent from the original baseline [Tabs] CREATE TABLE but
        // is added by the 04.05.04 upgrade
        // (ALTER TABLE Tabs ADD IsSecure bit NOT NULL CONSTRAINT ... DEFAULT(0))
        // and populated by 04.09.00 (update Tabs set IsSecure = 1); the
        // AddTab / UpdateTab stored procedures pass it. Therefore MAP it.
        builder.Property(t => t.IsSecure);                         // [IsSecure] bit NOT NULL (added by 04.05.04 upgrade)

        // MIGRATION: HasChildren / AuthorizedRoles / AdministratorRoles were
        // computed / permission-derived at runtime (not physical Tabs columns);
        // mapped as scalars for Phase-1 fidelity, no schema change (ADR-002). They
        // are settable auto-properties on the entity, so mapping them does NOT
        // break the model build, and it preserves lossless round-trip under the
        // InMemory provider used by Gate 5. They are deliberately NOT Ignored, and
        // carry no DDL length so no HasMaxLength is applied.
        builder.Property(t => t.HasChildren);
        builder.Property(t => t.AuthorizedRoles);
        builder.Property(t => t.AdministratorRoles);

        // MIGRATION: Tab (principal) -> TabPermissions (dependent) one-to-many.
        // The legacy TabInfo exposed a Security.Permissions.TabPermissionCollection;
        // here it is an EF navigation collection. WithOne() declares NO inverse
        // navigation on TabPermission. The foreign key is [TabID] on the
        // TabPermission table (legacy FK_TabPermission_Tabs -> Tabs.TabID).
        // TabPermission itself is detached from the Permission inheritance
        // hierarchy and mapped as an independent root entity in
        // PermissionConfiguration (HasBaseType((Type?)null)); EF merges that
        // dependent-side configuration with this principal-side relationship.
        // OnDelete(NoAction) is used deliberately: DNN tabs are SOFT-deleted
        // (IsDeleted), so a hard cascade delete of permissions is never the
        // operative path, and NoAction keeps the assembled model free of
        // SQL-Server multiple-cascade-path conflicts. The InMemory provider
        // ignores delete behavior, so Gate 5 is unaffected.
        builder.HasMany(t => t.TabPermissions)
               .WithOne()
               .HasForeignKey(tp => tp.TabID)
               .OnDelete(DeleteBehavior.NoAction);
    }
}
