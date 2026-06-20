using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// MIGRATION: EF Core 8 Fluent API mapping for the physical [TabModules] placement table, replacing the
// legacy ADO.NET / SqlDataProvider stored-procedure layer (GetTabModules / AddTabModule / UpdateTabModule)
// together with the reflection-based CBO.FillObject hydration. Per ADR-002 the existing DotNetNuke 4.9.0.85
// schema is mapped UNCHANGED: no migrations, no schema generation, no EnsureCreated, no data migration.
//
// [TabModules] is the physical home of the module-placement columns (TabID, ModuleOrder, PaneName,
// Visibility, ...) that the legacy flattened ModuleInfo denormalized onto the module object and that CP2
// ModuleConfiguration therefore Ignore()s on the Module entity. Mapping this table here lets ModuleRepository
// JOIN [TabModules] -> [Modules] to reproduce GetTabModules without querying the ignored Module members.

/// <summary>
/// Entity Framework Core configuration that maps the <see cref="TabModule"/> POCO entity onto the
/// pre-existing, unchanged DotNetNuke <c>dbo.TabModules</c> table.
/// </summary>
/// <remarks>
/// <para>
/// The class is discovered automatically by <c>DnnDbContext.OnModelCreating</c> through
/// <c>ModelBuilder.ApplyConfigurationsFromAssembly</c>. No relationship navigations are configured: the
/// repository joins <c>[TabModules]</c> to <c>[Modules]</c> explicitly by <c>ModuleID</c>, so no inverse
/// collection is added to <see cref="Module"/> and no cascade path is introduced.
/// </para>
/// <para>
/// Schema-fidelity rules (ADR-002): the table and column names are preserved verbatim, the integer primary
/// key retains its database <c>IDENTITY</c> semantics via the EF <c>ValueGeneratedOnAdd</c> convention (which
/// also enables key generation under the in-memory provider used by the integration-test fixtures), and no
/// SQL-Server-specific defaults, computed columns, or raw SQL are configured so the model builds cleanly
/// against <c>Microsoft.EntityFrameworkCore.InMemory</c> as well as SQL Server.
/// </para>
/// </remarks>
public sealed class TabModuleConfiguration : IEntityTypeConfiguration<TabModule>
{
    /// <summary>
    /// Configures the <see cref="TabModule"/> entity against the physical <c>dbo.TabModules</c> table.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="TabModule"/> entity type.</param>
    public void Configure(EntityTypeBuilder<TabModule> builder)
    {
        // Physical table: dbo.TabModules (CREATE TABLE in DotNetNuke.Schema.SqlDataProvider). Schema-
        // qualified and named verbatim per ADR-002 - the table already exists and must not be regenerated.
        builder.ToTable("TabModules", "dbo");

        // Primary key: [TabModuleID] (int NOT NULL IDENTITY(1,1)). Left at the EF ValueGeneratedOnAdd
        // convention (NO ValueGeneratedNever) so the database IDENTITY is honored on SQL Server and key
        // values are still generated under the in-memory provider used by the gates.
        builder.HasKey(tm => tm.TabModuleID);

        // --- The real, physical [TabModules] columns (mapped verbatim from the schema DDL) ---
        builder.Property(tm => tm.TabModuleID).HasColumnName("TabModuleID");        // [TabModuleID]      int           NOT NULL IDENTITY(1,1) PK
        builder.Property(tm => tm.TabID).HasColumnName("TabID");                    // [TabID]            int           NOT NULL
        builder.Property(tm => tm.ModuleID).HasColumnName("ModuleID");              // [ModuleID]         int           NOT NULL
        builder.Property(tm => tm.PaneName).HasColumnName("PaneName");              // [PaneName]         nvarchar(50)  NOT NULL
        builder.Property(tm => tm.ModuleOrder).HasColumnName("ModuleOrder");        // [ModuleOrder]      int           NOT NULL
        builder.Property(tm => tm.CacheTime).HasColumnName("CacheTime");            // [CacheTime]        int           NOT NULL
        builder.Property(tm => tm.Alignment).HasColumnName("Alignment");           // [Alignment]        nvarchar(10)  NULL
        builder.Property(tm => tm.Color).HasColumnName("Color");                    // [Color]            nvarchar(20)  NULL
        builder.Property(tm => tm.Border).HasColumnName("Border");                  // [Border]           nvarchar(1)   NULL
        builder.Property(tm => tm.IconFile).HasColumnName("IconFile");              // [IconFile]         nvarchar(100) NULL
        builder.Property(tm => tm.Visibility).HasColumnName("Visibility");          // [Visibility]       int           NOT NULL (projected to VisibilityState by the repository)
        builder.Property(tm => tm.ContainerSrc).HasColumnName("ContainerSrc");      // [ContainerSrc]     nvarchar(200) NULL
        builder.Property(tm => tm.DisplayTitle).HasColumnName("DisplayTitle");      // [DisplayTitle]     bit           NOT NULL
        builder.Property(tm => tm.DisplayPrint).HasColumnName("DisplayPrint");      // [DisplayPrint]     bit           NOT NULL
        builder.Property(tm => tm.DisplaySyndicate).HasColumnName("DisplaySyndicate"); // [DisplaySyndicate] bit        NOT NULL
    }
}
