using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent configuration for the <see cref="TabModule"/> entity, mapped to the existing DNN
/// <c>TabModules</c> table (module placement on a tab/page).
/// </summary>
// MIGRATION (SCHEMA FIDELITY — finding #2): restores the real relational shape that the legacy
// denormalized [vw_Modules] VIEW hid. The module-PLACEMENT fields (pane, order, cache time, alignment,
// colour, border, icon, visibility, container, display title/print/syndicate) are physical columns of
// [TabModules], NOT [Modules]; previously they were mapped onto [Modules], inventing phantom columns.
// This configuration maps them to their real home under verbatim legacy names and wires the two
// foreign keys to [Modules] and [Tabs] with their verbatim constraint names, preserving DATA MODEL
// FIDELITY against the existing schema (no table structures altered). Column set / nullability / key /
// FK names verified against the [TabModules] CREATE TABLE and its constraints in
// DotNetNuke.Schema.SqlDataProvider. Auto-discovered and applied by
// DnnDbContext.OnModelCreating via ModelBuilder.ApplyConfigurationsFromAssembly.
//
// The EF Core InMemory provider used by the integration tests (Validation Gate 5) ignores
// ToTable/HasColumnName/HasConstraintName and delete behaviours, but honours the key and the FK
// property shape; the relational (SQL Server) provider used by the API host honours all of them.
public class TabModuleConfiguration : IEntityTypeConfiguration<TabModule>
{
    /// <summary>
    /// Configures the <see cref="TabModule"/> entity type against the existing <c>TabModules</c> schema.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<TabModule> builder)
    {
        // MIGRATION: legacy table [TabModules] (PK [PK_{objectQualifier}TabModules] CLUSTERED on
        // [TabModuleID], IDENTITY(1,1)).
        builder.ToTable("TabModules");
        builder.HasKey(e => e.TabModuleID);
        builder.Property(e => e.TabModuleID).HasColumnName("TabModuleID").ValueGeneratedOnAdd();

        // --- The 15 real [TabModules] columns, mapped 1:1 under their verbatim legacy names ---
        builder.Property(e => e.TabID).HasColumnName("TabID");
        builder.Property(e => e.ModuleID).HasColumnName("ModuleID");
        builder.Property(e => e.PaneName).HasColumnName("PaneName");
        builder.Property(e => e.ModuleOrder).HasColumnName("ModuleOrder");
        builder.Property(e => e.CacheTime).HasColumnName("CacheTime");
        builder.Property(e => e.Alignment).HasColumnName("Alignment");
        builder.Property(e => e.Color).HasColumnName("Color");
        builder.Property(e => e.Border).HasColumnName("Border");
        builder.Property(e => e.IconFile).HasColumnName("IconFile");
        builder.Property(e => e.Visibility).HasColumnName("Visibility");
        builder.Property(e => e.ContainerSrc).HasColumnName("ContainerSrc");
        builder.Property(e => e.DisplayTitle).HasColumnName("DisplayTitle");
        builder.Property(e => e.DisplayPrint).HasColumnName("DisplayPrint");
        builder.Property(e => e.DisplaySyndicate).HasColumnName("DisplaySyndicate");

        // MIGRATION: [FK_{objectQualifier}TabModules_{objectQualifier}Modules]
        // (TabModules.ModuleID -> Modules.ModuleID, ON DELETE CASCADE). Modelled navigation-less
        // (neither side exposes a navigation) with the verbatim legacy constraint name preserved.
        builder.HasOne<Module>()
            .WithMany()
            .HasForeignKey(e => e.ModuleID)
            .HasConstraintName("FK_TabModules_Modules")
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: [FK_{objectQualifier}TabModules_{objectQualifier}Tabs]
        // (TabModules.TabID -> Tabs.TabID, ON DELETE CASCADE). Modelled navigation-less with the
        // verbatim legacy constraint name preserved.
        builder.HasOne<Tab>()
            .WithMany()
            .HasForeignKey(e => e.TabID)
            .HasConstraintName("FK_TabModules_Tabs")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
