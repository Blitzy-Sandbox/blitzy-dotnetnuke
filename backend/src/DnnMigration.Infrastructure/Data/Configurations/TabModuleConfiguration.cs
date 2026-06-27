using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: [QA-FINAL Issue #3/#4 — CRITICAL] Fluent API mapping for TabModule, the WRITE command-model over the
// EXISTING legacy [TabModules] table (DotNetNuke.Schema.SqlDataProvider). Schema NOT altered. [TabModules] holds the
// per-page module PLACEMENT that the vw_Modules read view LEFT-joins onto [Modules]. Because those placement columns
// (TabId/PaneName/ModuleOrder/CacheTime/Alignment/Color/Border/IconFile/Visibility/ContainerSrc/DisplayTitle/
// DisplayPrint/DisplaySyndicate) do NOT exist on the physical [Modules] table, they are persisted here; ModuleRepository
// stages a TabModule alongside each placed Module so the write fans out to [Modules] + [TabModules] in one SaveChanges.
public sealed class TabModuleConfiguration : IEntityTypeConfiguration<TabModule>
{
    public void Configure(EntityTypeBuilder<TabModule> builder)
    {
        builder.ToTable("TabModules");

        // [TabModules].[TabModuleID] int IDENTITY(1,1) PRIMARY KEY (store-generated).
        builder.HasKey(tm => tm.TabModuleId);
        builder.Property(tm => tm.TabModuleId)
            .HasColumnName("TabModuleID")
            .ValueGeneratedOnAdd();

        // Legacy column names preserved exactly (DotNetNuke.Schema.SqlDataProvider casing).
        builder.Property(tm => tm.TabId).HasColumnName("TabID");
        builder.Property(tm => tm.ModuleId).HasColumnName("ModuleID");
        builder.Property(tm => tm.PaneName).HasColumnName("PaneName");
        builder.Property(tm => tm.ModuleOrder).HasColumnName("ModuleOrder");
        builder.Property(tm => tm.CacheTime).HasColumnName("CacheTime");
        builder.Property(tm => tm.Alignment).HasColumnName("Alignment");
        builder.Property(tm => tm.Color).HasColumnName("Color");
        builder.Property(tm => tm.Border).HasColumnName("Border");
        builder.Property(tm => tm.IconFile).HasColumnName("IconFile");
        builder.Property(tm => tm.Visibility).HasColumnName("Visibility");
        builder.Property(tm => tm.ContainerSrc).HasColumnName("ContainerSrc");
        builder.Property(tm => tm.DisplayTitle).HasColumnName("DisplayTitle");
        builder.Property(tm => tm.DisplayPrint).HasColumnName("DisplayPrint");
        builder.Property(tm => tm.DisplaySyndicate).HasColumnName("DisplaySyndicate");

        // MIGRATION: TabModule -> Module (FK = TabModule.ModuleId references [Modules].[ModuleID]). WithMany() because
        // Module has no inverse TabModule collection (minimal blast radius). Cascade so removing a module removes its
        // placement rows. Module is mapped to [Modules] (write) + vw_Modules (read); the FK binds to the [Modules]
        // table key on the relational side. TabId is left a plain scalar (no Tab navigation) to avoid coupling a new
        // relationship into the Tab aggregate in this phase.
        builder.HasOne(tm => tm.Module)
            .WithMany()
            .HasForeignKey(tm => tm.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
