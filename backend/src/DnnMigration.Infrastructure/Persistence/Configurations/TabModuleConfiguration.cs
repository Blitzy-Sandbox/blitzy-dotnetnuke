// =============================================================================
// TabModuleConfiguration.cs
// -----------------------------------------------------------------------------
// EF Core 8 Fluent API mapping for the TabModule entity — the per-tab PLACEMENT
// of a module on a page — onto the EXISTING (unchanged) DotNetNuke 4.9.0.85
// dbo.TabModules table.
//
// MIGRATION (CP3 schema-fidelity correction): the legacy ModuleInfo fat object
// carried the TabModules placement fields (TabModuleID/TabID/PaneName/ModuleOrder/
// CacheTime/Alignment/Color/Border/IconFile/Visibility/ContainerSrc/DisplayTitle/
// DisplayPrint/DisplaySyndicate). Those properties are Ignore()'d on the Module
// entity in ModuleConfiguration.cs because they are NOT physical dbo.Modules
// columns (ADR-002). They are physical columns of dbo.TabModules, which THIS
// configuration maps. The ModuleRepository reads placement via an explicit
// TabModules->Modules join (GetByTabAsync) and persists placement through this
// entity (AddTabModule/UpdateTabModule semantics from
// Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb). Recorded in
// MIGRATION_NOTES.md §4.2 (Modules) and Deviation Index D-030/D-031.
//
// ADR-002 (schema preservation): the schema is mapped UNCHANGED — no EF
// migrations, no schema generation, no SQL-Server-only defaults, no data
// migration. Table and column names are reproduced verbatim from the baseline
// install DDL (Website/Providers/DataProviders/SqlDataProvider/
// DotNetNuke.Schema.SqlDataProvider, table TabModules). Every mapping primitive
// used here (ToTable / HasKey / Property / HasMaxLength / IsRequired) is
// InMemory-provider safe; no HasDefaultValueSql / HasComputedColumnSql / raw SQL
// is used, and the integer primary key is left at the EF convention default
// (ValueGeneratedOnAdd) which maps to the database IDENTITY column for SQL Server
// and enables automatic key generation under InMemory. (Do NOT call
// ValueGeneratedNever.) Mapped as an INDEPENDENT entity: TabID/ModuleID are plain
// scalar FK columns with NO navigation, so the model gains no cascade path and the
// repository performs explicit joins.
// =============================================================================

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core <see cref="IEntityTypeConfiguration{TEntity}"/> that maps the
/// <see cref="TabModule"/> entity onto the pre-existing DotNetNuke 4.9.0.85
/// <c>dbo.TabModules</c> table. Discovered automatically by
/// <c>DnnDbContext.OnModelCreating</c> via
/// <c>ApplyConfigurationsFromAssembly</c>, so it requires no explicit registration.
/// </summary>
public sealed class TabModuleConfiguration : IEntityTypeConfiguration<TabModule>
{
    /// <summary>Maps <see cref="TabModule"/> onto <c>dbo.TabModules</c>.</summary>
    /// <param name="builder">The entity type builder for <see cref="TabModule"/>.</param>
    public void Configure(EntityTypeBuilder<TabModule> builder)
    {
        // Map onto the existing physical table (default install schema = dbo).
        // ADR-002: the table already exists; this only describes the mapping and
        // never triggers schema generation.
        builder.ToTable("TabModules", "dbo");

        // Primary key. TabModuleID is an IDENTITY(1,1) column in the DNN 4.9 schema
        // ([TabModuleID] int NOT NULL IDENTITY(1,1)). Leaving the int-PK convention
        // intact preserves ValueGeneratedOnAdd so the InMemory provider can
        // auto-assign keys on insert. (Do NOT call ValueGeneratedNever.)
        builder.HasKey(tm => tm.TabModuleID);

        // ---------------------------------------------------------------------
        // Physical dbo.TabModules columns (verbatim DNN 4.9.0.85 schema names).
        // Property names already equal their column names, so HasColumnName is
        // unnecessary; the explicit declarations make this the single source of
        // truth for the column set. Lengths/nullability reproduce the DDL for
        // fidelity (InMemory ignores them; SQL Server honors them).
        // ---------------------------------------------------------------------
        builder.Property(tm => tm.TabModuleID);                 // [TabModuleID] int NOT NULL IDENTITY(1,1)
        builder.Property(tm => tm.TabID);                       // [TabID] int NOT NULL
        builder.Property(tm => tm.ModuleID);                    // [ModuleID] int NOT NULL
        builder.Property(tm => tm.PaneName)                     // [PaneName] nvarchar(50) NOT NULL
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(tm => tm.ModuleOrder);                 // [ModuleOrder] int NOT NULL
        builder.Property(tm => tm.CacheTime);                   // [CacheTime] int NOT NULL
        builder.Property(tm => tm.Alignment).HasMaxLength(10);  // [Alignment] nvarchar(10) NULL
        builder.Property(tm => tm.Color).HasMaxLength(20);      // [Color] nvarchar(20) NULL
        builder.Property(tm => tm.Border).HasMaxLength(1);      // [Border] nvarchar(1) NULL
        builder.Property(tm => tm.IconFile).HasMaxLength(100);  // [IconFile] nvarchar(100) NULL

        // MIGRATION: [Visibility] int NOT NULL <- VisibilityState enum. EF maps the
        // enum to its underlying int by convention; the ordinal is preserved verbatim.
        builder.Property(tm => tm.Visibility);                  // [Visibility] int NOT NULL

        builder.Property(tm => tm.ContainerSrc).HasMaxLength(200); // [ContainerSrc] nvarchar(200) NULL

        // MIGRATION: [DisplayTitle]/[DisplayPrint]/[DisplaySyndicate] bit NOT NULL
        // DEFAULT(1). The SQL-Server DEFAULT constraints are intentionally NOT
        // declared (no HasDefaultValueSql) to stay InMemory-provider safe; the
        // repository always sets these explicitly when persisting a placement.
        builder.Property(tm => tm.DisplayTitle);                // [DisplayTitle] bit NOT NULL
        builder.Property(tm => tm.DisplayPrint);                // [DisplayPrint] bit NOT NULL
        builder.Property(tm => tm.DisplaySyndicate);            // [DisplaySyndicate] bit NOT NULL
    }
}
